using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

/// <summary>
/// Read-only "where is this tender and who acts next" summary shown on every role's tender page,
/// so each person sees the whole cycle and whether the next move is theirs. It only reads existing
/// state (tender status, locked preparation, agreed weights, report step); it never changes anything.
/// </summary>
public static class TenderJourney
{
    public static readonly string[] Stages =
    [
        "Request", "Preparation", "Weights", "Publish", "Bidding", "Compliance", "Evaluation", "Approvals", "Result"
    ];

    public sealed record State(int Stage, string Actor, string[] ActorRoles, string Action, bool Finished, DateTime? DueUtc = null);

    static readonly string[] Officer = ["SourcingOfficer", "ProcurementOfficer"];
    static readonly string[] Manager = ["SourcingManager"];
    static readonly string[] Top = ["TopCommittee"];
    static readonly string[] TechFin = ["TechnicalRepresentative", "FinancialRepresentative"];
    static readonly string[] Supplier = ["Supplier"];

    public static async Task<State?> ComputeAsync(ApplicationDbContext db, int tenderId)
    {
        var tender = await db.Tenders.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenderId);
        if (tender == null) return null;
        var report = await db.EvaluationReports.AsNoTracking().Include(r => r.Votes).Include(r => r.Tender)
            .Where(r => r.TenderId == tenderId).OrderByDescending(r => r.Round).FirstOrDefaultAsync();
        var step = report == null ? TenderResultFlow.Step.None : await TenderResultFlow.StepOf(db, report);
        var weightsAgreed = CommitteeWeighting.Agree(CommitteeWeighting.ReadProposals(tender.EvaluationCriteria)) != null;
        return Compute(tender, step, weightsAgreed, DateTime.UtcNow);
    }

    public static State Compute(Tender tender, TenderResultFlow.Step step, bool weightsAgreed, DateTime utcNow)
    {
        // Report hand-offs come first: they decide who acts while the tender waits on approvals.
        switch (step)
        {
            case TenderResultFlow.Step.AtOfficerToForward:
                return new(7, "Sourcing officer", Officer, "Forward the committee report to the sourcing manager.", false);
            case TenderResultFlow.Step.AtManagerReview:
                return new(7, "Sourcing manager", Manager, "Approve, return or reject the committee report.", false);
            case TenderResultFlow.Step.AtTopCommittee:
                return new(7, "Top committee", Top, "Each of the four members votes on the recommended supplier.", false);
            case TenderResultFlow.Step.AtManagerToForwardDecision:
                return new(7, "Sourcing manager", Manager, "Forward the Top Committee decision to the sourcing officer.", false);
            case TenderResultFlow.Step.AtOfficerToAnnounce:
                return new(8, "Sourcing officer", Officer, "Announce the result to the suppliers.", false);
            case TenderResultFlow.Step.AtOfficerToSendBack:
                return new(7, "Sourcing officer", Officer, "Send the tender back to the evaluation committee for a new round.", false);
        }

        return tender.Status switch
        {
            TenderStatus.Draft when tender.EvaluationCriteriaLockedAt == null =>
                new(1, "Sourcing officer", Officer, "Complete the draft, invite suppliers, then click “Request pre-publication review”.", false),
            TenderStatus.Draft when !weightsAgreed =>
                new(2, "Technical + financial members", TechFin, "Each member submits the technical / financial weight split.", false),
            TenderStatus.Draft =>
                new(3, "Sourcing officer", Officer, "Weights are agreed. Publish the tender to send the invitations.", false),
            TenderStatus.Published when tender.SubmissionDeadline > utcNow =>
                new(4, "Invited suppliers", Supplier, "Accept the invitation and submit an offer before the deadline:", false, tender.SubmissionDeadline),
            TenderStatus.Published =>
                new(5, "Sourcing officer", Officer, "The deadline has passed. Close bidding.", false),
            TenderStatus.Closed when tender.OpeningDate > utcNow =>
                new(5, "Sourcing officer", Officer, "Bidding is closed. Offers unseal at the opening date:", false, tender.OpeningDate),
            TenderStatus.Closed =>
                new(5, "Sourcing officer", Officer, "Mark each offer compliant or not, then forward to evaluation.", false),
            TenderStatus.UnderEvaluation =>
                new(6, "Evaluation committee", [.. TechFin, "SourcingManager", "DepartmentRepresentative"],
                    "Technical + financial choose the same bid; then the manager and department representative accept it.", false),
            TenderStatus.AwaitingManagerReview or TenderStatus.AwaitingCommitteeDecision =>
                new(7, "Approvals", [], "The report is moving through the manager and the Top Committee.", false),
            TenderStatus.Awarded => new(8, "Done", [], "The tender is awarded and the suppliers were told the result.", true),
            TenderStatus.Cancelled => new(8, "Done", [], "The tender was closed without an award.", true),
            _ => new(1, "Sourcing officer", Officer, "Continue preparing the tender.", false)
        };
    }
}

/// <summary>Where the person whose turn it is should go to act (null when the page itself is the place).</summary>
public static class TenderJourneyLinks
{
    public static string? For(TenderJourney.State state, int tenderId, int? reportId) => state.Stage switch
    {
        _ when state.Finished => null,
        2 => $"/EvaluationCommittee/Evaluate/{tenderId}",
        5 when state.Action.StartsWith("Mark") => $"/SourcingOfficer/Offers/{tenderId}",
        6 => $"/EvaluationCommittee/Evaluate/{tenderId}",
        7 when state.ActorRoles.Contains("SourcingManager") && reportId != null => $"/SourcingManager/Details/{reportId}",
        7 when state.ActorRoles.Contains("TopCommittee") => $"/TopCommittee/Details/{tenderId}",
        _ when state.ActorRoles.Contains("SourcingOfficer") => $"/SourcingOfficer/Details/{tenderId}",
        _ => null
    };
}

/// <summary>Renders the journey bar: <c>@await Component.InvokeAsync("TenderJourney", new { tenderId })</c>.</summary>
public class TenderJourneyViewComponent(ApplicationDbContext db) : ViewComponent
{
    /// <param name="yourTurn">Pages that know the member's own progress (e.g. already voted) pass it to override the role guess.</param>
    public async Task<IViewComponentResult> InvokeAsync(int tenderId, bool? yourTurn = null, string? actor = null, string? action = null, string? anchor = null)
    {
        if (await TenderJourney.ComputeAsync(db, tenderId) is not { } state) return Content("");
        if (actor != null || action != null) state = state with { Actor = actor ?? state.Actor, Action = action ?? state.Action };
        ViewData["YourTurn"] = yourTurn;
        ViewData["Anchor"] = anchor;
        var reportId = await db.EvaluationReports.Where(r => r.TenderId == tenderId).OrderByDescending(r => r.Round)
            .Select(r => (int?)r.Id).FirstOrDefaultAsync();
        ViewData["Link"] = TenderJourneyLinks.For(state, tenderId, reportId);
        return View(state);
    }
}
