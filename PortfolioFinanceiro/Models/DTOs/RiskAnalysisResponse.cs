namespace PortfolioFinanceiro.Models.DTOs
{
    public class RiskAnalysisResponse
    {
        public required string OverallRisk { get; init; }
        public decimal? SharpeRatio { get; init; }
        public decimal RiskFreeRate { get; init; }
        public required ConcentrationRisk ConcentrationRisk { get; init; }
        public IReadOnlyList<SectorDiversification> SectorDiversification { get; init; } = [];
        public IReadOnlyList<string> Recommendations { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }
    public class ConcentrationRisk
    {
        public required PositionWeight LargestPosition { get; init; }
        public decimal Top3Concentration { get; init; }
        public decimal HerfindahlIndex { get; init; }
        public decimal EffectiveNumberOfPositions { get; init; }
    }

    public class PositionWeight
    {
        public required string Symbol { get; init; }
        public decimal Percentage { get; init; }
    }

    public class SectorDiversification
    {
        public required string Sector { get; init; }
        public decimal Percentage { get; init; }
        public required string Risk { get; init; }
        public IReadOnlyList<string> Symbols { get; init; } = [];
    }
}
