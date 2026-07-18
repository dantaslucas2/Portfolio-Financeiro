using PortfolioFinanceiro.Models;
using PortfolioFinanceiro.Models.DTOs;
using PortfolioFinanceiro.Services.Interfaces;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;

namespace PortfolioFinanceiro.Services
{
    internal sealed class RiskAnalyzer : IRiskAnalyzer
    {
        private const decimal HighPositionThreshold = 0.25m;
        private const decimal MediumPositionThreshold = 0.15m;
        private const decimal HighSectorThreshold = 0.40m;
        private const decimal MediumSectorThreshold = 0.25m;

        private const string High = "High";
        private const string Medium = "Medium";
        private const string Low = "Low";

        private readonly IPortfolioValuator _valuator;
        private readonly ILogger<RiskAnalyzer> _logger;
        public RiskAnalyzer(IPortfolioValuator valuator, ILogger<RiskAnalyzer> logger)
        {
            _valuator = valuator;
            _logger = logger;
        }
        public async Task<RiskAnalysisResponse?> AnalyzeAsync(int portfolioId, CancellationToken ct = default)
        {
            var valued = await _valuator.ValueAsync(portfolioId, ct);
            if (valued is null)
                return null;

            var totalReturn = valued.TotalReturn;
            var annualizedReturn = totalReturn is null ? null : FinancialMath.Annualize(totalReturn.Value, valued.PeriodInDays);
            var volatility = PerformanceCalculator.CalculatePortfolioVolatility(valued);
            var sharpe = FinancialMath.SharpeRatio(annualizedReturn, volatility, valued.Market.SelicRate);

            var warnings = valued.Warnings.ToList();
            if (sharpe is null)
            {
                warnings.Add(
                    "O índice de Sharpe não pôde ser calculado: depende de retorno anualizado e volatilidade, " +
                    "e ao menos um deles é indisponível ou nulo para este portfólio.");
            }
            else if (valued.VolatilityCoverage < 1m)
            {
                warnings.Add(
                    $"O índice de Sharpe usa uma volatilidade estimada sobre {FinancialMath.ToPercent(valued.VolatilityCoverage)}% " +
                    "do valor do portfólio e deve ser lido como aproximação.");
            }

            var sectors = BuildSectorDiversification(valued);
            var concentration = BuildConcentrationRisk(valued);
            var overallRisk = DetermineOverallRisk(valued, sectors);

            _logger.LogDebug(
                "Portfólio {PortfolioId}: risco {Risk}, HHI {Hhi:F4}, top3 {Top3:P2}, Sharpe {Sharpe} (retorno anual {Return:P4}, vol {Vol:P4}, Selic {Selic:P2})",
                portfolioId, overallRisk, concentration.HerfindahlIndex, concentration.Top3Concentration / 100m,
                sharpe, annualizedReturn, volatility, valued.Market.SelicRate);

            return new RiskAnalysisResponse
            {
                OverallRisk = overallRisk,
                SharpeRatio = sharpe is null ? null : Math.Round(sharpe.Value, 4),
                RiskFreeRate = FinancialMath.ToPercent(valued.Market.SelicRate),
                ConcentrationRisk = concentration,
                SectorDiversification = sectors,
                Recommendations = BuildRecommendations(valued, sectors),
                Warnings = warnings
            };
        }
        private static ConcentrationRisk BuildConcentrationRisk(ValuedPortfolio valued)
        {
            var ordered = valued.Positions.OrderByDescending(p => p.Weight).ToList();
            var largest = ordered.FirstOrDefault();
            var hhi = FinancialMath.HerfindahlIndex(valued.Positions.Select(p => p.Weight));

            return new ConcentrationRisk
            {
                LargestPosition = new PositionWeight
                {
                    Symbol = largest?.Symbol ?? "N/A",
                    Percentage = FinancialMath.ToPercent(largest?.Weight ?? 0m)
                },
                // Se houver menos de três posições, soma as existentes: o portfólio inteiro é o "top 3".
                Top3Concentration = FinancialMath.ToPercent(ordered.Take(3).Sum(p => p.Weight)),
                HerfindahlIndex = Math.Round(hhi, 4),
                EffectiveNumberOfPositions = hhi > 0 ? Math.Round(1m / hhi, 2) : 0m
            };
        }

        private static List<SectorDiversification> BuildSectorDiversification(ValuedPortfolio valued) =>
            valued.Positions
                .GroupBy(p => p.Sector)
                .Select(g =>
                {
                    var weight = g.Sum(p => p.Weight);
                    return new SectorDiversification
                    {
                        Sector = g.Key,
                        Percentage = FinancialMath.ToPercent(weight),
                        Risk = ClassifySector(weight),
                        Symbols = g.Select(p => p.Symbol).OrderBy(s => s).ToList()
                    };
                })
                .OrderByDescending(s => s.Percentage)
                .ToList();

        private static string ClassifySector(decimal weight) => weight switch
        {
            > HighSectorThreshold => High,
            >= MediumSectorThreshold => Medium,
            _ => Low
        };

        private static string ClassifyPosition(decimal weight) => weight switch
        {
            > HighPositionThreshold => High,
            >= MediumPositionThreshold => Medium,
            _ => Low
        };
        private static string DetermineOverallRisk(ValuedPortfolio valued, List<SectorDiversification> sectors)
        {
            if (valued.Positions.Count == 0)
                return Low;

            var levels = valued.Positions.Select(p => ClassifyPosition(p.Weight))
                .Concat(sectors.Select(s => s.Risk))
                .ToList();

            if (levels.Contains(High)) return High;
            if (levels.Contains(Medium)) return Medium;
            return Low;
        }

        private static List<string> BuildRecommendations(ValuedPortfolio valued, List<SectorDiversification> sectors)
        {
            var recommendations = new List<string>();

            foreach (var sector in sectors.Where(s => s.Risk is High or Medium))
            {
                var limit = sector.Risk == High ? HighSectorThreshold : MediumSectorThreshold;
                recommendations.Add(
                    $"Reduzir exposição ao setor {sector.Sector} ({sector.Percentage}%): acima do limite de " +
                    $"{FinancialMath.ToPercent(limit)}% para risco {sector.Risk.ToLowerInvariant()}.");
            }

            foreach (var position in valued.Positions.Where(p => p.Weight >= MediumPositionThreshold).OrderByDescending(p => p.Weight))
            {
                recommendations.Add(
                    $"Posição {position.Symbol} representa {FinancialMath.ToPercent(position.Weight)}% do portfólio " +
                    $"(ideal < {FinancialMath.ToPercent(MediumPositionThreshold)}%).");
            }

            // Count >= 2 evita a recomendação óbvia e inútil num portfólio de um único ativo,
            // onde o HHI é sempre 1 e "1 posição efetiva" não é uma informação acionável.
            var effective = FinancialMath.HerfindahlIndex(valued.Positions.Select(p => p.Weight));
            if (effective > 0 && 1m / effective < 5m && valued.Positions.Count >= 2)
            {
                recommendations.Add(
                    $"Diversificação equivalente a apenas {Math.Round(1m / effective, 2)} posições igualmente ponderadas; " +
                    "considere distribuir melhor o capital entre os ativos.");
            }

            if (recommendations.Count == 0)
                recommendations.Add("Nenhum risco relevante de concentração identificado: posições e setores estão dentro dos limites definidos.");

            return recommendations;
        }
    }
}
