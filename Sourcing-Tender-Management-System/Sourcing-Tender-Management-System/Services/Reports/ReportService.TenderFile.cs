using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;

namespace Sourcing_Tender_Management_System.Services.Reports;

public partial class ReportService
{
    /// <summary>
    /// The complete story of one tender on one page: the request behind it, invited suppliers, every bid with its
    /// cost breakdown, evaluation scores, compliance checks, recommendations, manager and committee decisions,
    /// clarifications and the timeline. Read-only.
    /// </summary>
    public async Task<ReportDocument?> BuildTenderFileAsync(int tenderId, ReportViewer viewer)
    {
        var t = await db.Tenders.AsNoTracking().Where(x => x.Id == tenderId)
            .Select(x => new
            {
                x.Id,
                x.TenderNumber,
                x.Title,
                x.Description,
                Category = x.Category.Name,
                x.Status,
                x.CreatedAt,
                x.PublishedAt,
                x.SubmissionDeadline,
                x.OpeningDate,
                x.EstimatedValue,
                x.TechnicalWeight,
                x.FinancialWeight,
                x.RequiresTopCommittee,
                CreatedBy = x.CreatedByUser.FullName,
                x.ProcurementRequestId,
                RequestNumber = x.ProcurementRequest != null ? x.ProcurementRequest.RequestNumber : null,
                RequestTitle = x.ProcurementRequest != null ? x.ProcurementRequest.Title : null,
                Department = x.ProcurementRequest != null ? x.ProcurementRequest.Department.Name : null,
                RequestedBy = x.ProcurementRequest != null ? x.ProcurementRequest.RequestedByUser.FullName : null,
                RequestedAt = x.ProcurementRequest != null ? x.ProcurementRequest.RequestedAt : (DateTime?)null,
                RequestBudget = x.ProcurementRequest != null ? x.ProcurementRequest.EstimatedBudget : (decimal?)null,
                x.AwardedBidId,
                AwardedSupplier = x.AwardedBid != null ? x.AwardedBid.Supplier.CompanyName : null,
                AwardedAmount = x.AwardedBid != null ? x.AwardedBid.TotalAmount : (decimal?)null,
                AwardedCurrency = x.AwardedBid != null ? x.AwardedBid.Currency : null
            })
            .FirstOrDefaultAsync();

        if (t is null) return null;

        var items = await db.TenderItems.AsNoTracking().Where(i => i.TenderId == tenderId).OrderBy(i => i.Id)
            .Select(i => new { i.ItemName, i.Quantity, i.Unit, i.Specifications }).ToListAsync();

        var invitations = await db.TenderInvitations.AsNoTracking().Where(i => i.TenderId == tenderId)
            .OrderBy(i => i.Supplier.CompanyName)
            .Select(i => new { Supplier = i.Supplier.CompanyName, i.Status, i.InvitedAt, i.RespondedAt, i.ResponseReason })
            .ToListAsync();

        var bids = await db.Bids.AsNoTracking().Where(b => b.TenderId == tenderId && b.Status != BidStatus.Draft)
            .OrderBy(b => b.TotalAmount)
            .Select(b => new
            {
                b.Id,
                Supplier = b.Supplier.CompanyName,
                b.BidNumber,
                b.Status,
                b.ComplianceStatus,
                b.TotalAmount,
                b.Currency,
                b.TaxAmount,
                b.FeesAmount,
                b.ShippingAmount,
                b.InstallationAmount,
                b.MaintenanceAmount,
                b.DeliveryDays,
                b.PaymentTerms,
                b.ValidUntil,
                b.SubmittedAt,
                b.DisqualificationReason
            })
            .ToListAsync();

        var criteria = await db.EvaluationCriteria.AsNoTracking().Where(c => c.TenderId == tenderId)
            .OrderBy(c => c.Type).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.Name, c.Type, c.Weight, c.MaximumScore }).ToListAsync();

        var criterionScores = await db.BidEvaluationScores.AsNoTracking()
            .Where(s => s.BidEvaluation.Bid.TenderId == tenderId)
            .Select(s => new { s.BidEvaluation.BidId, s.EvaluationCriterionId, s.Score })
            .ToListAsync();

        var compliance = await db.BidComplianceReviews.AsNoTracking().Where(r => r.Bid.TenderId == tenderId)
            .OrderBy(r => r.ReviewedAt)
            .Select(r => new { Supplier = r.Bid.Supplier.CompanyName, r.Decision, r.Reason, Reviewer = r.ReviewedByUser.FullName, r.ReviewedAt })
            .ToListAsync();

        var reports = await db.EvaluationReports.AsNoTracking().Where(r => r.TenderId == tenderId)
            .OrderBy(r => r.Round)
            .Select(r => new
            {
                r.Id,
                r.Round,
                r.Status,
                Recommended = r.RecommendedBid != null ? r.RecommendedBid.Supplier.CompanyName : null,
                r.RecommendationReason,
                PreparedBy = r.PreparedByUser.FullName,
                r.CreatedAt,
                r.SubmittedAt
            })
            .ToListAsync();

        var reportScores = await db.EvaluationReportBids.AsNoTracking().Where(x => x.EvaluationReport.TenderId == tenderId)
            .Select(x => new
            {
                x.EvaluationReport.Round,
                Supplier = x.Bid.Supplier.CompanyName,
                x.TechnicalScore,
                x.FinancialScore,
                x.WeightedTotal,
                x.IsCompliant,
                x.Notes
            })
            .ToListAsync();

        var approvals = await db.Approvals.AsNoTracking().Where(a => a.TenderId == tenderId)
            .OrderBy(a => a.Round).ThenBy(a => a.Sequence)
            .Select(a => new { a.Round, a.Stage, a.ApprovalLevel, Approver = a.ApproverUser.FullName, a.Status, a.Comments, a.DecidedAt })
            .ToListAsync();

        var votes = await db.CommitteeVotes.AsNoTracking().Where(v => v.EvaluationReport.TenderId == tenderId)
            .OrderBy(v => v.EvaluationReport.Round).ThenBy(v => v.CommitteeType).ThenBy(v => v.MemberUser.FullName)
            .Select(v => new { v.EvaluationReport.Round, v.CommitteeType, Member = v.MemberUser.FullName, v.Decision, v.Reason, v.DecidedAt })
            .ToListAsync();

        var clarifications = await db.Clarifications.AsNoTracking().Where(c => c.TenderId == tenderId)
            .OrderBy(c => c.AskedAt)
            .Select(c => new { c.Subject, AskedBy = c.AskedByUser.FullName, c.Status, c.AskedAt, c.ResolvedAt, Replies = c.Responses.Count })
            .ToListAsync();

        var timeline = await db.WorkflowTransitions.AsNoTracking()
            .Where(w => w.TenderId == tenderId || (t.ProcurementRequestId != null && w.ProcurementRequestId == t.ProcurementRequestId))
            .OrderBy(w => w.CreatedAt)
            .Select(w => new
            {
                w.CreatedAt,
                w.FromStage,
                w.ToStage,
                w.Action,
                By = w.PerformedByUser.FullName,
                AssignedTo = w.AssignedToUser != null ? w.AssignedToUser.FullName : null,
                w.Reason
            })
            .ToListAsync();

        // ---- document
        var doc = new ReportDocument
        {
            Kind = ReportKind.Tenders,
            Title = $"Tender file: {t.TenderNumber}",
            Description = t.Title,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = viewer.DisplayName,
            FilterSummary = $"Tender {t.TenderNumber}"
        };

        var currencies = bids.Select(b => b.Currency).Distinct().ToList();
        var currency = currencies.Count == 1 ? currencies[0] : string.Empty;
        string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

        doc.Tiles.Add(new("Status", Pretty(t.Status)));
        doc.Tiles.Add(new("Estimated value", Money(t.EstimatedValue)));
        doc.Tiles.Add(new("Invited suppliers", invitations.Count.ToString()));
        doc.Tiles.Add(new("Submitted bids", bids.Count.ToString()));
        doc.Tiles.Add(new("Lowest bid", bids.Count == 0 ? "-" : Money(bids.Min(b => b.TotalAmount))));
        if (t.AwardedAmount is decimal awarded)
        {
            var saving = t.EstimatedValue - awarded;
            doc.Tiles.Add(new("Awarded amount", Money(awarded)));
            doc.Tiles.Add(new("Difference from estimate", t.EstimatedValue == 0
                ? Money(saving)
                : $"{Money(saving)} ({(100.0 * (double)saving / (double)t.EstimatedValue).ToString("0.0", CultureInfo.InvariantCulture)}%)"));
        }

        var overview = new ReportTable
        {
            Title = "Tender overview",
            Note = "A positive difference means the awarded amount is below the estimated value.",
            Columns = new[] { "Field", "Value" }
        };
        overview.Add("Tender number", t.TenderNumber);
        overview.Add("Title", t.Title);
        overview.Add("Description", t.Description);
        overview.Add("Category", t.Category);
        overview.Add("Status", Pretty(t.Status));
        overview.Add("Created by", t.CreatedBy);
        overview.Add("Created", t.CreatedAt);
        overview.Add("Published", t.PublishedAt);
        overview.Add("Submission deadline", t.SubmissionDeadline);
        overview.Add("Opening date", t.OpeningDate);
        overview.Add("Estimated value", Money(t.EstimatedValue));
        overview.Add("Evaluation weights", $"Technical {t.TechnicalWeight:0.##}% / Financial {t.FinancialWeight:0.##}%");
        overview.Add("Requires Top Committee", t.RequiresTopCommittee ? "Yes" : "No");
        overview.Add("Awarded supplier", t.AwardedSupplier);
        doc.Tables.Add(overview);

        if (t.RequestNumber is not null)
        {
            var request = new ReportTable { Title = "Purchase request behind this tender", Columns = new[] { "Field", "Value" } };
            request.Add("Request number", t.RequestNumber);
            request.Add("Title", t.RequestTitle);
            request.Add("Department", t.Department);
            request.Add("Requested by", t.RequestedBy);
            request.Add("Requested", t.RequestedAt);
            request.Add("Estimated budget", t.RequestBudget is decimal budget ? Money(budget) : null);
            doc.Tables.Add(request);
        }

        var itemTable = new ReportTable { Title = "Items", Columns = new[] { "Item", "Quantity", "Unit", "Specifications" } };
        foreach (var i in items) itemTable.Add(i.ItemName, i.Quantity, i.Unit, i.Specifications);
        doc.Tables.Add(itemTable);

        var invited = new ReportTable
        {
            Title = "Invited suppliers",
            Columns = new[] { "Supplier", "Invitation", "Invited", "Responded", "Response reason" }
        };
        foreach (var i in invitations) invited.Add(i.Supplier, Pretty(i.Status), i.InvitedAt, i.RespondedAt, i.ResponseReason);
        doc.Tables.Add(invited);

        var bidTable = new ReportTable
        {
            Title = "Bids and cost breakdown",
            Note = "Cheapest first. Tax, fees, shipping, installation and maintenance are shown as the supplier entered them.",
            Columns = new[]
            {
                "Supplier", "Bid", "Status", "Compliance", "Total amount", "Currency", "Tax", "Fees", "Shipping",
                "Installation", "Maintenance", "Delivery (days)", "Payment terms", "Valid until", "Submitted"
            }
        };
        foreach (var b in bids)
            bidTable.Add(b.Supplier, b.BidNumber, Pretty(b.Status), Pretty(b.ComplianceStatus), b.TotalAmount, b.Currency, b.TaxAmount,
                b.FeesAmount, b.ShippingAmount, b.InstallationAmount, b.MaintenanceAmount, b.DeliveryDays, b.PaymentTerms, b.ValidUntil, b.SubmittedAt);
        doc.Tables.Add(bidTable);

        var criteriaTable = new ReportTable
        {
            Title = "Evaluation criteria",
            Columns = new[] { "Criterion", "Type", "Weight", "Maximum score" }
        };
        foreach (var c in criteria) criteriaTable.Add(c.Name, Pretty(c.Type), c.Weight, c.MaximumScore);
        doc.Tables.Add(criteriaTable);

        if (criterionScores.Count > 0 && criteria.Count > 0)
        {
            var matrix = new ReportTable
            {
                Title = "Average score per criterion",
                Note = "Average of all evaluators' scores for each bid.",
                Columns = new[] { "Supplier" }.Concat(criteria.Select(c => c.Name)).ToArray()
            };
            foreach (var b in bids)
            {
                var cells = new List<object?> { b.Supplier };
                foreach (var c in criteria)
                {
                    var scores = criterionScores.Where(s => s.BidId == b.Id && s.EvaluationCriterionId == c.Id).Select(s => s.Score).ToList();
                    cells.Add(scores.Count == 0 ? null : Math.Round(scores.Average(), 2));
                }
                matrix.Add(cells.ToArray());
            }
            doc.Tables.Add(matrix);
        }

        var scoreTable = new ReportTable
        {
            Title = "Unified report scores",
            Columns = new[] { "Round", "Supplier", "Technical", "Financial", "Weighted total", "Compliant", "Notes" }
        };
        foreach (var s in reportScores.OrderBy(s => s.Round).ThenByDescending(s => s.WeightedTotal))
            scoreTable.Add(s.Round, s.Supplier, s.TechnicalScore, s.FinancialScore, s.WeightedTotal, s.IsCompliant ? "Yes" : "No", s.Notes);
        doc.Tables.Add(scoreTable);

        var complianceTable = new ReportTable
        {
            Title = "Compliance checks",
            Columns = new[] { "Supplier", "Result", "Reason", "Reviewed by", "Reviewed" }
        };
        foreach (var c in compliance) complianceTable.Add(c.Supplier, Pretty(c.Decision), c.Reason, c.Reviewer, c.ReviewedAt);
        doc.Tables.Add(complianceTable);

        var excluded = bids.Where(b => b.Status is BidStatus.Disqualified or BidStatus.Rejected or BidStatus.NonCompliant
                                       || b.ComplianceStatus == ComplianceDecision.NonCompliant).ToList();
        var excludedTable = new ReportTable { Title = "Excluded offers", Columns = new[] { "Supplier", "Status", "Compliance", "Reason" } };
        foreach (var b in excluded) excludedTable.Add(b.Supplier, Pretty(b.Status), Pretty(b.ComplianceStatus), b.DisqualificationReason);
        doc.Tables.Add(excludedTable);

        var recTable = new ReportTable
        {
            Title = "Committee recommendations",
            Columns = new[] { "Round", "Status", "Recommended supplier", "Reason", "Prepared by", "Created", "Submitted" }
        };
        foreach (var r in reports) recTable.Add(r.Round, Pretty(r.Status), r.Recommended, r.RecommendationReason, r.PreparedBy, r.CreatedAt, r.SubmittedAt);
        doc.Tables.Add(recTable);

        var approvalTable = new ReportTable
        {
            Title = "Approval decisions",
            Columns = new[] { "Round", "Stage", "Approver", "Decision", "Comments", "Decided" }
        };
        foreach (var a in approvals)
            approvalTable.Add(a.Round, a.Stage is null ? a.ApprovalLevel : Pretty(a.Stage.Value), a.Approver, Pretty(a.Status), a.Comments, a.DecidedAt);
        doc.Tables.Add(approvalTable);

        var voteTable = new ReportTable
        {
            Title = "Committee votes",
            Columns = new[] { "Round", "Committee", "Member", "Vote", "Justification", "Voted" }
        };
        foreach (var v in votes) voteTable.Add(v.Round, v.CommitteeType == CommitteeType.Top ? "Top Committee" : "Evaluation committee", v.Member, Pretty(v.Decision), v.Reason, v.DecidedAt);
        doc.Tables.Add(voteTable);

        var clarificationTable = new ReportTable
        {
            Title = "Clarifications",
            Columns = new[] { "Subject", "Asked by", "Status", "Asked", "Resolved", "Replies" }
        };
        foreach (var c in clarifications) clarificationTable.Add(c.Subject, c.AskedBy, Pretty(c.Status), c.AskedAt, c.ResolvedAt, c.Replies);
        doc.Tables.Add(clarificationTable);

        var timelineTable = new ReportTable
        {
            Title = "Timeline",
            Note = "Every step of the request and the tender, oldest first.",
            Columns = new[] { "When (UTC)", "From", "To", "Action", "By", "Assigned to", "Reason" }
        };
        foreach (var w in timeline)
            timelineTable.Add(w.CreatedAt, Pretty(w.FromStage), Pretty(w.ToStage), Pretty(w.Action), w.By, w.AssignedTo, w.Reason);
        doc.Tables.Add(timelineTable);

        doc.AddChart("Bid amount by supplier", bids.Select(b => (b.Supplier, b.TotalAmount)), currency);
        var latestRound = reportScores.Count == 0 ? 0 : reportScores.Max(s => s.Round);
        doc.AddChart($"Weighted score by supplier (round {latestRound})",
            reportScores.Where(s => s.Round == latestRound).OrderByDescending(s => s.WeightedTotal).Select(s => (s.Supplier, s.WeightedTotal)));
        doc.AddChart("Delivery time by supplier (days)", bids.Select(b => (b.Supplier, (decimal)b.DeliveryDays)));

        return doc;
    }
}
