using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

/// <summary>
/// Pure rules for the free "AI" bid analysis: a weighted score from the agreed technical and
/// financial weights. No database, no network; the same input always gives the same answer.
/// </summary>
public static class BidScoring
{
    public sealed record BidScore(
        int BidId, decimal Technical, decimal Financial, decimal Total, int Rank,
        List<string> Strengths, List<string> Weaknesses);

    public sealed record Result(int RecommendedBidId, List<BidScore> Ranking, string Summary);

    // Bid.TotalAmount already includes tax, fees, shipping, installation and maintenance
    // (the supplier offer form adds them), so it is the total cost. Do not add them again.
    public static decimal TotalCost(Bid b) => b.TotalAmount;

    public static Result? Analyze(
        IReadOnlyList<Bid> bids, int tenderItemCount, decimal technicalWeight, decimal financialWeight)
    {
        if (bids.Count == 0) return null;

        decimal lowestCost = bids.Select(TotalCost).Where(c => c > 0).DefaultIfEmpty(0).Min();
        int fastest = bids.Min(b => b.DeliveryDays);
        decimal averageCost = bids.Average(TotalCost);

        var scored = bids.Select(b =>
        {
            decimal cost = TotalCost(b);
            decimal financial = cost <= 0 || lowestCost <= 0 ? 0 : Math.Round(100m * lowestCost / cost, 2);
            decimal technical = TechnicalScore(b, tenderItemCount);
            decimal total = Math.Round((technicalWeight * technical + financialWeight * financial) / 100m, 2);
            return (Bid: b, Cost: cost, Technical: technical, Financial: financial, Total: total);
        })
        .OrderByDescending(x => x.Total)
        .ThenBy(x => x.Cost)            // tie-break: cheaper wins
        .ThenBy(x => x.Bid.DeliveryDays) // then faster
        .ToList();

        var ranking = scored.Select((x, i) =>
        {
            var (strengths, weaknesses) = Explain(x.Bid, x.Cost, x.Technical, lowestCost, averageCost, fastest);
            return new BidScore(x.Bid.Id, x.Technical, x.Financial, x.Total, i + 1, strengths, weaknesses);
        }).ToList();

        var best = scored[0];
        string summary = $"Bid {best.Bid.BidNumber} ranks first with {best.Total:0.0}/100 " +
                         $"(technical {technicalWeight:0.##}%, financial {financialWeight:0.##}%).";
        return new Result(best.Bid.Id, ranking, summary);
    }

    private static decimal TechnicalScore(Bid b, int tenderItemCount)
    {
        var scores = b.Evaluations.Where(e => e.TotalScore > 0).Select(e => e.TotalScore).ToList();
        if (scores.Count > 0) return Math.Round(Math.Min(100m, scores.Average()), 2);   // committee scores
        if (tenderItemCount == 0) return 0;
        return Math.Round(Math.Min(100m, 100m * b.Items.Count / tenderItemCount), 2);   // fallback: item coverage
    }

    private static (List<string> Strengths, List<string> Weaknesses) Explain(
        Bid b, decimal cost, decimal technical, decimal lowestCost, decimal averageCost, int fastest)
    {
        var strengths = new List<string>();
        var weaknesses = new List<string>();

        if (lowestCost > 0 && cost == lowestCost) strengths.Add("Lowest total cost.");
        else if (lowestCost > 0) weaknesses.Add($"{(cost - lowestCost) / lowestCost:P0} more expensive than the cheapest bid.");

        if (averageCost > 0)
        {
            decimal vsAverage = (cost - averageCost) / averageCost;
            if (vsAverage <= -0.05m) strengths.Add($"{-vsAverage:P0} below the average cost.");
            else if (vsAverage >= 0.05m) weaknesses.Add($"{vsAverage:P0} above the average cost.");
        }

        if (b.DeliveryDays == fastest) strengths.Add("Fastest delivery.");
        else weaknesses.Add($"Delivery is {b.DeliveryDays - fastest} days slower than the fastest bid.");

        if (technical >= 80) strengths.Add("Strong technical score.");
        else if (technical < 50) weaknesses.Add("Weak technical score.");
        return (strengths, weaknesses);
    }
}
