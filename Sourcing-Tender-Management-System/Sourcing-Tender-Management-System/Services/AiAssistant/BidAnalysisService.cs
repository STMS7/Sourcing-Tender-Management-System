using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services.AiAssistant;

/// <param name="IncludePendingCompliance">
/// True for the sourcing officer (compliance may not be finished): every submitted bid except non-compliant ones.
/// False for the committees: compliant bids only.
/// </param>
/// <param name="RecommendedBidId">The bid the sourcing manager sent to the top committee, if any.</param>
public sealed record BidAnalysisRequest(
    int TenderId, BidAnalysisAudience Audience, string UserId,
    bool IncludePendingCompliance, int? RecommendedBidId = null);

public interface IBidAnalysisService
{
    /// <summary>
    /// Scores the bids in C# and asks the local model to explain them. The caller must check that the user
    /// may see the tender's bids first. Returns null when the tender does not exist.
    /// </summary>
    Task<BidAnalysisViewModel?> AnalyzeAsync(BidAnalysisRequest request, CancellationToken ct = default);
}

public class BidAnalysisService(
    ApplicationDbContext db,
    IOllamaClient ollama,
    IOptions<OllamaOptions> options,
    ILogger<BidAnalysisService> logger) : IBidAnalysisService
{
    public const string ScoringMethod = "weighted-scoring-v1";

    public async Task<BidAnalysisViewModel?> AnalyzeAsync(BidAnalysisRequest request, CancellationToken ct = default)
    {
        var tender = await db.Tenders.AsNoTracking()
            .Where(t => t.Id == request.TenderId)
            .Select(t => new { t.TenderNumber, t.TechnicalWeight, t.FinancialWeight, ItemCount = t.Items.Count })
            .FirstOrDefaultAsync(ct);
        if (tender is null) return null;

        var model = new BidAnalysisViewModel
        {
            TenderNumber = tender.TenderNumber,
            Audience = request.Audience,
            TechnicalWeight = tender.TechnicalWeight,
            FinancialWeight = tender.FinancialWeight,
            AnalyzedAt = DateTime.UtcNow
        };

        var query = db.Bids.AsNoTracking()
            .Where(b => b.TenderId == request.TenderId && b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn);
        query = request.IncludePendingCompliance
            ? query.Where(b => b.ComplianceStatus != ComplianceDecision.NonCompliant)
            : query.Where(b => b.ComplianceStatus == ComplianceDecision.Compliant);
        var bids = await query.Include(b => b.Supplier).Include(b => b.Items).Include(b => b.Evaluations)
            .AsSplitQuery().OrderBy(b => b.Id).ToListAsync(ct);

        var result = BidScoring.Analyze(bids, tender.ItemCount, tender.TechnicalWeight, tender.FinancialWeight);
        if (result is null)
        {
            model.Message = request.IncludePendingCompliance
                ? "There are no submitted offers to analyze (non-compliant offers are left out)."
                : "There are no compliant bids to analyze.";
            return model;
        }

        var byId = bids.ToDictionary(b => b.Id);
        model.TopScoredBidId = result.RecommendedBidId;
        model.RecommendedBidId = request.RecommendedBidId is int r && byId.ContainsKey(r) ? r : null;
        model.ScoringSummary = result.Summary;
        model.TechnicalFromCommitteeScores = bids.Any(b => b.Evaluations.Any(e => e.TotalScore > 0));
        model.Rows = result.Ranking.Select((s, i) =>
        {
            var bid = byId[s.BidId];
            return new BidAnalysisRow
            {
                BidId = bid.Id, Label = BidAnalysisPrompt.LabelFor(i), Rank = s.Rank,
                SupplierName = bid.Supplier.CompanyName, BidNumber = bid.BidNumber,
                TotalAmount = bid.TotalAmount, Currency = bid.Currency, DeliveryDays = bid.DeliveryDays,
                Compliance = bid.ComplianceStatus, Technical = s.Technical, Financial = s.Financial, Total = s.Total,
                Strengths = s.Strengths, Weaknesses = s.Weaknesses
            };
        }).ToList();

        var currencies = bids.Select(b => b.Currency).Distinct().ToList();
        if (currencies.Count > 1)
            model.Warnings.Add($"Bids use different currencies ({string.Join(", ", currencies)}). No exchange rate is applied, so the price comparison is not reliable.");
        if (request.IncludePendingCompliance && bids.Any(b => b.ComplianceStatus != ComplianceDecision.Compliant))
            model.Warnings.Add("Some offers have not passed the compliance review yet.");
        if (!model.TechnicalFromCommitteeScores)
            model.Warnings.Add("No committee technical scores yet: the technical score is the share of tender items each bid quoted.");

        await AddNarrativeAsync(model, byId, tender.ItemCount, ct);
        await SaveAsync(model, request, ct);
        return model;
    }

    private async Task AddNarrativeAsync(
        BidAnalysisViewModel model, IReadOnlyDictionary<int, Bid> bids, int itemCount, CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            model.NarrativeUnavailableReason = "The local AI model is turned off in settings. The scores below are still valid.";
            return;
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, options.Value.TimeoutSeconds)));
            var json = await ollama.ChatJsonAsync(
                BidAnalysisPrompt.System(model.Audience),
                BidAnalysisPrompt.User(model, bids, itemCount),
                BidAnalysisPrompt.Schema(),
                timeout.Token);
            var narrative = BidAnalysisPrompt.Parse(json, model.Rows.Select(r => r.Label).ToList());
            if (narrative is null)
            {
                logger.LogWarning("Ollama answer could not be used: {Answer}", json.Length > 500 ? json[..500] : json);
                model.NarrativeUnavailableReason = "The AI model gave an answer that could not be read. Try again.";
                return;
            }
            model.Narrative = narrative;
            model.ModelName = ollama.ModelName;
            foreach (var row in model.Rows)
                if (narrative.BidNotes.TryGetValue(row.Label, out var note)) row.AssistantNote = note;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException
                                   && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ollama bid analysis failed");
            model.NarrativeUnavailableReason = ex is OperationCanceledException
                ? "The AI model took too long to answer. The scores below are still valid."
                : "The AI model is not reachable. Start Ollama and try again. The scores below are still valid.";
        }
    }

    // One row per bid and page: a new analysis replaces the previous one.
    private async Task SaveAsync(BidAnalysisViewModel model, BidAnalysisRequest request, CancellationToken ct)
    {
        var key = $"assistant/{request.Audience}";
        var bidIds = model.Rows.Select(r => r.BidId).ToList();
        var existing = await db.BidAnalyses.Where(a => bidIds.Contains(a.BidId) && a.ModelName == key).ToListAsync(ct);
        foreach (var row in model.Rows)
        {
            var saved = existing.FirstOrDefault(a => a.BidId == row.BidId);
            if (saved is null)
            {
                saved = new BidAnalysis { BidId = row.BidId, ModelName = key };
                db.BidAnalyses.Add(saved);
            }
            saved.RequestedByUserId = request.UserId;
            saved.AnalyzedAt = model.AnalyzedAt;
            saved.ExtractedDataJson = JsonSerializer.Serialize(new
            {
                Scoring = ScoringMethod, Llm = model.ModelName, row.Rank, row.Technical, row.Financial, row.Total,
                TopScored = row.BidId == model.TopScoredBidId
            });
            saved.Strengths = Trim(string.Join(" ", row.Strengths));
            saved.Weaknesses = Trim(string.Join(" ", row.Weaknesses));
            saved.PreliminaryRecommendation = Trim(row.AssistantNote ?? (row.BidId == model.TopScoredBidId ? model.ScoringSummary : $"Ranked {row.Rank}."));
        }
        await db.SaveChangesAsync(ct);
    }

    private static string? Trim(string text) => text.Length == 0 ? null : text.Length <= 3000 ? text : text[..3000];
}
