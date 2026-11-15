namespace Sourcing_Tender_Management_System.Models;

/// <summary>Which page asked for the analysis; it changes what the assistant focuses on.</summary>
public enum BidAnalysisAudience
{
    /// <summary>Sourcing officer: shortlist the better offers before compliance and evaluation.</summary>
    SourcingOfficer,
    /// <summary>Evaluation committee (technical, financial, department, sourcing manager): compare compliant bids.</summary>
    EvaluationCommittee,
    /// <summary>Top committee: judge the bid the sourcing manager recommended against the others.</summary>
    TopCommittee
}

/// <summary>Result shown in the "AI assistant" popup. Advisory only: people make every decision.</summary>
public class BidAnalysisViewModel
{
    public string TenderNumber { get; set; } = string.Empty;
    public BidAnalysisAudience Audience { get; set; }
    public decimal TechnicalWeight { get; set; }
    public decimal FinancialWeight { get; set; }

    /// <summary>Set when no analysis can be made (for example no bids).</summary>
    public string? Message { get; set; }

    /// <summary>Highest weighted score. A suggestion, never a decision.</summary>
    public int? TopScoredBidId { get; set; }
    /// <summary>The bid the sourcing manager sent to the top committee, when there is one.</summary>
    public int? RecommendedBidId { get; set; }
    public string ScoringSummary { get; set; } = string.Empty;
    public bool TechnicalFromCommitteeScores { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public List<BidAnalysisRow> Rows { get; set; } = [];
    /// <summary>Facts the reader must know before trusting the scores (for example mixed currencies).</summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>Text written by the local model; null when it was not available.</summary>
    public BidAnalysisNarrative? Narrative { get; set; }
    public string? NarrativeUnavailableReason { get; set; }
    public string? ModelName { get; set; }
}

public class BidAnalysisRow
{
    public int BidId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string BidNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int DeliveryDays { get; set; }
    public ComplianceDecision Compliance { get; set; }
    public decimal Technical { get; set; }
    public decimal Financial { get; set; }
    public decimal Total { get; set; }
    public List<string> Strengths { get; set; } = [];
    public List<string> Weaknesses { get; set; } = [];
    /// <summary>The model's short note about this bid, if any.</summary>
    public string? AssistantNote { get; set; }
}

/// <summary>The floating "AI assistant" button and popup placed on a page.</summary>
public class BidAnalysisButtonViewModel
{
    /// <summary>POST endpoint that returns the _BidAnalysis partial.</summary>
    public string Url { get; set; } = string.Empty;
    public string TenderNumber { get; set; } = string.Empty;
}

/// <summary>The validated part of the local model's answer.</summary>
public class BidAnalysisNarrative
{
    public string Summary { get; set; } = string.Empty;
    public string? PreferredLabel { get; set; }
    public string PreferredReason { get; set; } = string.Empty;
    public List<string> Risks { get; set; } = [];
    public List<string> QuestionsToCheck { get; set; } = [];
    public Dictionary<string, string> BidNotes { get; set; } = [];
}
