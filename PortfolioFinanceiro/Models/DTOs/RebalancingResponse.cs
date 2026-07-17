namespace PortfolioFinanceiro.Models.DTOs
{
    public class RebalancingResponse
    {
        public bool NeedsRebalancing { get; init; }
        public decimal CurrentValue { get; init; }
        public decimal DeviationThreshold { get; init; }
        public decimal MinimumTradeValue { get; init; }
        public decimal TransactionCostRate { get; init; }
        public IReadOnlyList<AllocationDeviation> CurrentAllocation { get; init; } = [];
        public IReadOnlyList<SuggestedTrade> SuggestedTrades { get; init; } = [];
        public decimal TotalTransactionCost { get; init; }
        public required ExpectedImprovement ExpectedImprovement { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public class AllocationDeviation
    {
        public required string Symbol { get; init; }
        public decimal CurrentWeight { get; init; }
        public decimal TargetWeight { get; init; }
        public decimal Deviation { get; init; }
    }

    public class SuggestedTrade
    {
        public required string Symbol { get; init; }
        public required string Action { get; init; }
        public int Quantity { get; init; }
        public decimal EstimatedValue { get; init; }
        public decimal TransactionCost { get; init; }
        public decimal ResultingWeight { get; init; }
        public required string Reason { get; init; }
    }

    public class ExpectedImprovement 
    {
        public decimal TotalDeviationBefore { get; init; }
        public decimal TotalDeviationAfter { get; init; }
        public decimal DeviationReduction { get; init; }
        public required string Summary { get; init; }
    }
}
