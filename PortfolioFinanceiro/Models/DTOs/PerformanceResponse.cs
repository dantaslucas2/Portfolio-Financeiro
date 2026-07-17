namespace PortfolioFinanceiro.Models.DTOs
{
    public class PerformanceResponse
    {
        public decimal TotalInvestment { get; init; }
        public decimal InvestedAmount { get; init; }
        public decimal CurrentValue { get; init; }
        public decimal TotalReturn { get; init; }
        public decimal TotalReturnAmount { get; init; }
        public decimal TotalReturnOnInvestment { get; init; }
        public decimal? AnnualizedReturn { get; init; }
        public decimal? Volatility { get; init; }
        public decimal VolatilityCoverage { get; init; }
        public DateOnly AsOfDate { get; init; }
        public int PeriodInDays { get; init; }
        public IReadOnlyList<PositionPerformance> PositionsPerformance { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }
}
