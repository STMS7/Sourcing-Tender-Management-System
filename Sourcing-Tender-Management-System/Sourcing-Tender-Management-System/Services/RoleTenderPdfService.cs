using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

/// <summary>Builds a fresh, read-only tender brief for the audience carried by a signed QR grant.</summary>
public class RoleTenderPdfService(ApplicationDbContext db, IWebHostEnvironment environment)
{
    static RoleTenderPdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public async Task<(byte[] Pdf, string Reference)?> CreateAsync(
        int tenderId, TenderQrAudience audience, int? supplierId)
    {
        var tender = await db.Tenders.AsNoTracking()
            .Include(t => t.CreatedByUser).Include(t => t.Category)
            .Include(t => t.ProcurementRequest).ThenInclude(r => r!.Department)
            .Include(t => t.ProcurementRequest).ThenInclude(r => r!.RequestedByUser)
            .Include(t => t.Items).Include(t => t.DocumentRequirements)
            .Include(t => t.Invitations).ThenInclude(i => i.Supplier)
            .Include(t => t.CommitteeMembers).ThenInclude(m => m.User)
            .Include(t => t.Bids).ThenInclude(b => b.Supplier)
            .Include(t => t.Bids).ThenInclude(b => b.Items).ThenInclude(i => i.TenderItem)
            .Include(t => t.Bids).ThenInclude(b => b.Documents).ThenInclude(d => d.TenderDocumentRequirement)
            .Include(t => t.Bids).ThenInclude(b => b.Evaluations)
            .Include(t => t.EvaluationCriteria)
            .Include(t => t.EvaluationReports).ThenInclude(r => r.RecommendedBid).ThenInclude(b => b!.Supplier)
            .Include(t => t.EvaluationReports).ThenInclude(r => r.Votes).ThenInclude(v => v.MemberUser)
            .Include(t => t.AwardedBid).ThenInclude(b => b!.Supplier)
            .AsSplitQuery().FirstOrDefaultAsync(t => t.Id == tenderId);
        if (tender is null) return null;

        var history = await db.WorkflowTransitions.AsNoTracking().Include(w => w.PerformedByUser)
            .Where(w => w.TenderId == tenderId).OrderByDescending(w => w.CreatedAt).Take(40).ToListAsync();
        var analysisAudience = audience switch
        {
            TenderQrAudience.SourcingOfficer => BidAnalysisAudience.SourcingOfficer,
            TenderQrAudience.TopCommittee => BidAnalysisAudience.TopCommittee,
            _ => BidAnalysisAudience.EvaluationCommittee
        };
        var analyses = audience == TenderQrAudience.Supplier ? [] : await db.BidAnalyses.AsNoTracking()
            .Include(a => a.Bid).ThenInclude(b => b.Supplier)
            .Where(a => a.Bid.TenderId == tenderId && a.ModelName == $"assistant/{analysisAudience}")
            .OrderByDescending(a => a.AnalyzedAt).ToListAsync();
        var logo = await File.ReadAllBytesAsync(Path.Combine(environment.WebRootPath, "images", "zaytouna-logo.png"));
        var pdf = Build(tender, history, analyses, audience, supplierId, logo);
        return (pdf, tender.TenderNumber);
    }

    private static byte[] Build(Tender tender, List<WorkflowTransition> history, List<BidAnalysis> analyses,
        TenderQrAudience audience, int? supplierId, byte[] logo) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));
            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text($"{AudienceName(audience)} Live Tender Brief").FontSize(19).Bold().FontColor("#3D4A2A");
                        column.Item().Text($"{tender.TenderNumber}  •  generated {Utc(DateTime.UtcNow)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(105).Height(52).Image(logo).FitArea();
                });
                header.Item().PaddingTop(6).LineHorizontal(1).LineColor("#C9A84C");
            });
            page.Content().PaddingVertical(10).Column(content =>
            {
                content.Spacing(9);
                AddTitle(content, tender);
                AddOverview(content, tender, audience);
                AddFinalResult(content, tender);
                AddText(content, "Description", tender.Description);
                AddItems(content, tender.Items.OrderBy(i => i.Id));
                AddRequirements(content, tender.DocumentRequirements.OrderByDescending(r => r.IsMandatory));

                if (audience == TenderQrAudience.Supplier)
                    AddSupplierView(content, tender, supplierId);
                else
                {
                    if (audience is TenderQrAudience.SourcingOfficer or TenderQrAudience.SourcingManager)
                        AddInvitations(content, tender.Invitations.OrderBy(i => i.Supplier.CompanyName));
                    AddBids(content, tender, audience);
                    AddCommittee(content, tender.CommitteeMembers.OrderBy(m => m.Role));
                    AddReports(content, tender.EvaluationReports.OrderByDescending(r => r.Round), audience);
                    AddAnalysis(content, analyses);
                    AddHistory(content, history, audience);
                }
                AddText(content, "Terms and conditions", tender.TermsAndConditions);
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1));
                text.Span("Live read-only report • Advisory analysis never replaces an authorised decision • Page ");
                text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
            });
        });
    }).GeneratePdf();

    private static void AddTitle(ColumnDescriptor content, Tender tender)
    {
        content.Item().Background("#F5F0E1").Border(1).BorderColor("#E4DCC8").Padding(12).Column(c =>
        {
            c.Item().Text(tender.Title).FontSize(16).Bold().FontColor("#2E3920");
            c.Item().Text($"Status: {Words(tender.Status.ToString())}").Bold().FontColor("#56632E");
        });
    }

    private static void AddOverview(ColumnDescriptor content, Tender tender, TenderQrAudience audience)
    {
        Heading(content, "Tender overview");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
            Box(table, "Category", tender.Category?.Name ?? "-");
            Box(table, "Audience", AudienceName(audience));
            Box(table, "Submission deadline", Utc(tender.SubmissionDeadline));
            Box(table, "Bid opening", tender.OpeningDate is DateTime opening ? Utc(opening) : "After deadline");
            Box(table, "Estimated value", tender.EstimatedValue.ToString("N2", CultureInfo.InvariantCulture));
            Box(table, "Published", tender.PublishedAt is DateTime published ? Utc(published) : "Not published");
            Box(table, "Department", tender.ProcurementRequest?.Department?.Name ?? "Not linked");
            Box(table, "Source officer", Name(tender.CreatedByUser));
        });
    }

    private static void AddFinalResult(ColumnDescriptor content, Tender tender)
    {
        if (tender.Status == TenderStatus.Awarded && tender.AwardedBid is { } winner)
            content.Item().Background("#EAF4E6").BorderLeft(4).BorderColor("#2F6B2A").Padding(10)
                .Text($"FINAL AWARD  •  {winner.Supplier.CompanyName}  •  {winner.TotalAmount:N2} {winner.Currency}  •  {winner.DeliveryDays} delivery days").Bold();
        else if (tender.Status == TenderStatus.Cancelled)
            content.Item().Background("#F9E9E3").BorderLeft(4).BorderColor("#B4411E").Padding(10)
                .Text("FINAL RESULT  •  Tender closed without an award.").Bold();
    }

    private static void AddItems(ColumnDescriptor content, IEnumerable<TenderItem> items)
    {
        Heading(content, "Tender items");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
            Header(table, ["Item", "Specifications", "Quantity", "Unit"]);
            foreach (var item in items) Row(table, [item.ItemName, item.Specifications ?? "-", item.Quantity.ToString("N3"), item.Unit]);
        });
    }

    private static void AddRequirements(ColumnDescriptor content, IEnumerable<TenderDocumentRequirement> requirements)
    {
        var rows = requirements.ToList();
        if (rows.Count == 0) return;
        Heading(content, "Required documents");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(); });
            Header(table, ["Document", "Description", "Required"]);
            foreach (var r in rows) Row(table, [r.DocumentType, r.Description ?? "-", r.IsMandatory ? "Yes" : "Optional"]);
        });
    }

    private static void AddInvitations(ColumnDescriptor content, IEnumerable<TenderInvitation> invitations)
    {
        var rows = invitations.ToList();
        if (rows.Count == 0) return;
        Heading(content, "Supplier invitations");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(2); });
            Header(table, ["Supplier", "Status", "Invited", "Response"]);
            foreach (var i in rows) Row(table, [i.Supplier.CompanyName, Words(i.Status.ToString()), Utc(i.InvitedAt), i.ResponseReason ?? "-"]);
        });
    }

    private static void AddBids(ColumnDescriptor content, Tender tender, TenderQrAudience audience)
    {
        var bids = tender.Bids.Where(b => b.Status is not (BidStatus.Draft or BidStatus.Withdrawn));
        if (audience is TenderQrAudience.EvaluationCommittee or TenderQrAudience.TopCommittee)
            bids = bids.Where(b => b.ComplianceStatus == ComplianceDecision.Compliant);
        var rows = bids.OrderBy(b => b.TotalAmount).ToList();
        if (rows.Count == 0) return;
        Heading(content, audience == TenderQrAudience.TopCommittee ? "Decision-ready compliant offers" : "Submitted offers");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
            Header(table, ["Supplier", "Amount", "Delivery", "Compliance", "Status"]);
            foreach (var b in rows) Row(table, [b.Supplier.CompanyName, $"{b.TotalAmount:N2} {b.Currency}", $"{b.DeliveryDays} days",
                Words(b.ComplianceStatus.ToString()), Words(b.Status.ToString())], b.Id == tender.AwardedBidId);
        });
    }

    private static void AddCommittee(ColumnDescriptor content, IEnumerable<TenderCommitteeMember> members)
    {
        var rows = members.ToList();
        if (rows.Count == 0) return;
        Heading(content, "Assigned committee");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(); });
            Header(table, ["Role", "Member", "Committee"]);
            foreach (var m in rows) Row(table, [m.RoleType?.ToString() ?? m.Role, Name(m.User), m.CommitteeType.ToString()]);
        });
    }

    private static void AddReports(ColumnDescriptor content, IEnumerable<EvaluationReport> reports, TenderQrAudience audience)
    {
        var rows = reports.ToList();
        if (rows.Count == 0) return;
        Heading(content, "Evaluation and decision reports");
        foreach (var report in rows)
            content.Item().Border(1).BorderColor("#E4DCC8").Padding(8).Column(c =>
            {
                c.Item().Text($"Round {report.Round} • {Words(report.Status.ToString())}").Bold();
                c.Item().Text($"Recommended supplier: {report.RecommendedBid?.Supplier.CompanyName ?? "None"}");
                if (!string.IsNullOrWhiteSpace(report.Summary)) c.Item().Text(report.Summary);
                if (!string.IsNullOrWhiteSpace(report.RecommendationReason)) c.Item().Text("Reason: " + report.RecommendationReason);
                if (audience == TenderQrAudience.TopCommittee && report.Votes.Count > 0)
                    c.Item().Text("Top Committee votes: " + string.Join(" • ", report.Votes.Where(v => v.CommitteeType == CommitteeType.Top)
                        .GroupBy(v => v.Decision).Select(g => $"{Words(g.Key.ToString())} {g.Count()}"))).FontSize(8);
            });
    }

    private static void AddAnalysis(ColumnDescriptor content, List<BidAnalysis> analyses)
    {
        if (analyses.Count == 0)
        {
            Heading(content, "Decision support");
            content.Item().Background("#FFF8E6").Padding(8).Text("No saved AI narrative is available yet. Official live data above remains complete. Run AI analysis from the tender workspace to add advisory observations.");
            return;
        }
        Heading(content, "Latest AI-assisted decision support");
        content.Item().Text($"Advisory only • latest analysis {Utc(analyses.Max(a => a.AnalyzedAt))}").Italic().FontColor(Colors.Grey.Darken1);
        foreach (var a in analyses.GroupBy(a => a.BidId).Select(g => g.OrderByDescending(x => x.AnalyzedAt).First()).Take(10))
            content.Item().BorderLeft(3).BorderColor("#C9A84C").PaddingLeft(7).Column(c =>
            {
                c.Item().Text(a.Bid.Supplier.CompanyName).Bold();
                if (!string.IsNullOrWhiteSpace(a.PreliminaryRecommendation)) c.Item().Text(a.PreliminaryRecommendation);
                if (!string.IsNullOrWhiteSpace(a.Strengths)) c.Item().Text("Strengths: " + a.Strengths).FontSize(8);
                if (!string.IsNullOrWhiteSpace(a.Weaknesses)) c.Item().Text("Checks / risks: " + a.Weaknesses).FontSize(8);
            });
    }

    private static void AddHistory(ColumnDescriptor content, List<WorkflowTransition> history, TenderQrAudience audience)
    {
        var rows = audience == TenderQrAudience.TopCommittee ? history.Take(15).ToList() : history.Take(25).ToList();
        if (rows.Count == 0) return;
        Heading(content, "Workflow history");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(2); });
            Header(table, ["Date", "Stage", "Event"]);
            foreach (var h in rows) Row(table, [Utc(h.CreatedAt), $"{Words(h.FromStage.ToString())} → {Words(h.ToStage.ToString())}", h.Reason ?? Words(h.Action.ToString())]);
        });
    }

    private static void AddSupplierView(ColumnDescriptor content, Tender tender, int? supplierId)
    {
        var invitation = tender.Invitations.FirstOrDefault(i => i.SupplierId == supplierId);
        var bid = tender.Bids.FirstOrDefault(b => b.SupplierId == supplierId);
        Heading(content, "Your participation");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
            Box(table, "Invitation", invitation is null ? "No invitation" : Words(invitation.Status.ToString()));
            Box(table, "Offer status", bid is null ? "No offer submitted" : Words(bid.Status.ToString()));
            Box(table, "Compliance", bid is null ? "-" : Words(bid.ComplianceStatus.ToString()));
            Box(table, "Result", bid?.Status == BidStatus.Accepted ? "Selected supplier" : tender.Status == TenderStatus.Awarded ? "Another offer was selected" : "Pending final decision");
        });
        if (bid is null) return;
        Heading(content, "Your submitted offer");
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
            Box(table, "Total", $"{bid.TotalAmount:N2} {bid.Currency}"); Box(table, "Delivery", $"{bid.DeliveryDays} days");
            Box(table, "Valid until", bid.ValidUntil?.ToString("dd MMM yyyy") ?? "-"); Box(table, "Documents", bid.Documents.Count.ToString());
        });
        if (bid.Items.Count > 0)
        {
            content.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(2); });
                Header(table, ["Item", "Unit price", "Total", "Response"]);
                foreach (var i in bid.Items) Row(table, [i.TenderItem.ItemName, i.UnitPrice.ToString("N2"), i.TotalPrice.ToString("N2"), i.TechnicalResponse ?? i.BrandOrModel ?? "-"]);
            });
        }
    }

    private static void AddText(ColumnDescriptor content, string title, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Heading(content, title); content.Item().Text(text);
    }
    private static void Heading(ColumnDescriptor c, string text) => c.Item().PaddingTop(3).Text(text).FontSize(12).Bold().FontColor("#3D4A2A");
    private static void Header(TableDescriptor t, string[] values) { foreach (var v in values) t.Cell().Background("#3D4A2A").Padding(4).Text(v).Bold().FontColor(Colors.White); }
    private static void Row(TableDescriptor t, string[] values, bool highlight = false) { foreach (var v in values) t.Cell().Background(highlight ? "#EAF4E6" : Colors.White).BorderBottom(.5f).BorderColor("#E4DCC8").Padding(4).Text(v); }
    private static void Box(TableDescriptor t, string label, string value) => t.Cell().Border(1).BorderColor("#E4DCC8").Padding(6).Column(c => { c.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1); c.Item().Text(value).Bold(); });
    private static string AudienceName(TenderQrAudience audience) => audience switch { TenderQrAudience.SourcingOfficer => "Sourcing Officer", TenderQrAudience.SourcingManager => "Sourcing Manager", TenderQrAudience.EvaluationCommittee => "Evaluation Committee", TenderQrAudience.TopCommittee => "Top Committee", _ => "Supplier" };
    private static string Name(ApplicationUser? user) => user is null ? "-" : string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "-" : user.FullName;
    private static string Words(string value) => System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");
    private static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
