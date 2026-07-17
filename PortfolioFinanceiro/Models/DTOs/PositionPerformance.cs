namespace PortfolioFinanceiro.Models.DTOs
{
    public class PositionPerformance
    {
        public required string Symbol { get; init; }
        public decimal Quantity { get; init; }
        public decimal AveragePrice { get; init; }
        public decimal CurrentPrice { get; init; }
        public decimal InvestedAmount { get; init; }
        public decimal CurrentValue { get; init; }
        public decimal? Return { get; init; }
        public decimal Weight { get; init; }
        public decimal? Volatility { get; init; }

    }
}
