namespace Sourcing_Tender_Management_System.Services.Analytics;

/// <summary>Descriptive historical indicators, never a prediction or an eligibility decision.</summary>
public static class SupplierRiskCalculator
{
    public static SupplierRisk Calculate(int reviewedBids, int nonCompliantBids, int closedContracts, int terminatedContracts)
    {
        if (reviewedBids < 0 || closedContracts < 0 || nonCompliantBids < 0 || nonCompliantBids > reviewedBids ||
            terminatedContracts < 0 || terminatedContracts > closedContracts) throw new ArgumentOutOfRangeException(nameof(reviewedBids));
        var factors = new List<RiskFactor>();
        var availableWeight = (reviewedBids > 0 ? 60m : 0) + (closedContracts > 0 ? 40m : 0);
        if (availableWeight == 0) return new(null, "Insufficient data", 0, factors);
        void Add(string label, int adverse, int observed, decimal weight)
        {
            if (observed > 0) factors.Add(new(label, adverse, observed, weight / availableWeight * 100,
                adverse / (decimal)observed * weight / availableWeight * 100));
        }
        Add("Non-compliant bids", nonCompliantBids, reviewedBids, 60);
        Add("Terminated contracts", terminatedContracts, closedContracts, 40);
        var score = (int)Math.Round(factors.Sum(f => f.Contribution), MidpointRounding.AwayFromZero);
        return new(score, score >= 60 ? "High" : score >= 30 ? "Moderate" : "Low", reviewedBids + closedContracts, factors);
    }
}
