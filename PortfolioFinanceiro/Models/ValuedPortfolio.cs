namespace PortfolioFinanceiro.Models
{
    internal sealed record ValuedPortfolio
    {
        public required Portfolio Portfolio { get; init; }
        public required MarketSnapshot Market { get; init; }
        public required IReadOnlyList<ValuedPosition> Positions { get; init; }
        public decimal InvestedAmount { get; init; }
        public decimal CurrentValue { get; init; }
        public decimal VolatilityCoverage { get; init; }
        public int PeriodInDays { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public decimal? TotalReturn =>
    InvestedAmount > 0 ? (CurrentValue - InvestedAmount) / InvestedAmount : null;

    }

    internal sealed record ValuedPosition
    {
        public required Position Position { get; init; }
        public required Asset Asset { get; init; }
        public string Symbol => Asset.Symbol;
        public string Sector => Asset.Sector;
        public decimal InvestedAmount { get; init; }
        public decimal CurrentValue { get; init; }
        public decimal Weight { get; init; }
        public decimal TargetWeight { get; init; }
        public bool HasPriceHistory { get; init; }
        public decimal? Return =>
    Position.AveragePrice > 0 ? (Asset.CurrentPrice - Position.AveragePrice) / Position.AveragePrice : null;
    }
}
