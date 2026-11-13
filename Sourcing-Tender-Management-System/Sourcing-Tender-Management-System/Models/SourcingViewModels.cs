using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class SourcingDashboardViewModel
{
    public int IncomingRequests { get; set; }
    public int ApprovedRequests { get; set; }
    public int DraftTenders { get; set; }
    public int PublishedTenders { get; set; }
    public int PendingCompliance { get; set; }
    public List<Tender> RecentTenders { get; set; } = [];
    public List<UserTask> Tasks { get; set; } = [];
}

public class SourcingTenderForm
{
    public int Id { get; set; }
    [Range(1, int.MaxValue)] public int ProcurementRequestId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(3000)] public string? Description { get; set; }
    [Required, StringLength(4000), Display(Name = "Terms and conditions")] public string TermsAndConditions { get; set; } = "";
    [Range(1, int.MaxValue), Display(Name = "Category")] public int CategoryId { get; set; }
    [Required, Display(Name = "Submission deadline (UTC)")] public DateTime? SubmissionDeadline { get; set; }
    [Display(Name = "Bid opening date (UTC)")] public DateTime? OpeningDate { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99"), Display(Name = "Estimated value")] public decimal EstimatedValue { get; set; }
    [StringLength(2000), Display(Name = "Approval note (optional)")] public string? ReviewComment { get; set; }
}

public class SourcingRequestDecisionForm
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = "";
}

public class SourcingCommitteeForm
{
    [Range(1, int.MaxValue)] public int TenderId { get; set; }
    [Required] public string ManagerUserId { get; set; } = "";
    [Required] public string DepartmentUserId { get; set; } = "";
    [Required] public string TechnicalUserId { get; set; } = "";
    [Required] public string FinancialUserId { get; set; } = "";
}

public class SourcingCriterionForm
{
    public int TenderId { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(1000)] public string? Description { get; set; }
    [EnumDataType(typeof(CriterionType))] public CriterionType Type { get; set; }
    [Range(typeof(decimal), "0.01", "999.99")] public decimal MaximumScore { get; set; } = 100;
}

public class SourcingRequirementForm
{
    public int TenderId { get; set; }
    [Required, StringLength(100)] public string DocumentType { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
    public bool IsMandatory { get; set; } = true;
}

public class SourcingComplianceForm
{
    public int BidId { get; set; }
    [EnumDataType(typeof(ComplianceDecision))] public ComplianceDecision Decision { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = "";
}

public class SourcingClarificationForm
{
    public int? ProcurementRequestId { get; set; }
    public int? TenderId { get; set; }
    public int? BidId { get; set; }
    [Required, StringLength(450)] public string RecipientUserId { get; set; } = "";
    [Required, StringLength(200)] public string Subject { get; set; } = "";
    [Required, StringLength(2000)] public string Question { get; set; } = "";
}

public class SourcingMessageContext
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

public class SourcingMessagesViewModel
{
    public string Group { get; set; } = "RequestingDepartment";
    public string? Context { get; set; }
    public SourcingClarificationForm Form { get; set; } = new();
    public List<SourcingMessageContext> Contexts { get; set; } = [];
    public List<ApplicationUser> Recipients { get; set; } = [];
    public List<Clarification> Conversations { get; set; } = [];
    public Clarification? Selected { get; set; }
    public bool CanReply { get; set; }
}

public class SourcingRecipientMessagesViewModel
{
    public List<Clarification> Conversations { get; set; } = [];
    public Clarification? Selected { get; set; }
    public bool CanReply { get; set; }
}

public class SourcingEvaluationReadiness
{
    public List<string> Issues { get; set; } = [];
    public int SubmittedOffers { get; set; }
    public int CompliantOffers { get; set; }
    public bool Ready => Issues.Count == 0;
}
