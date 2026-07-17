using PortfolioFinanceiro.Data;
using PortfolioFinanceiro.Models;
using PortfolioFinanceiro.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PortfolioFinanceiro.Services
{
    internal sealed class PortfolioValuator : IPortfolioValuator
    {
        private const decimal AllocationTolerance = 0.001m;

        private readonly DataContext _db;
        private readonly ILogger<PortfolioValuator> _logger;

        public PortfolioValuator(DataContext db, ILogger<PortfolioValuator> logger)
        {
            _db = db;
            _logger = logger;
        }
        public async Task<ValuedPortfolio?> ValueAsync(int portfolioId, CancellationToken ct = default)
        {
            var portfolio = await _db.Portfolios
            .Include(p => p.Positions)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == portfolioId, ct);

            if (portfolio is null)
                return null;

            var market = await _db.MarketSnapshots.AsNoTracking().FirstAsync(ct);

            var symbols = portfolio.Positions.Select(p => p.AssetSymbol).ToList();
            var assets = await _db.Assets
                .Include(a => a.PriceHistory)
                .AsNoTracking()
                .Where(a => symbols.Contains(a.Symbol))
                .ToDictionaryAsync(a => a.Symbol, ct);

            var warnings = new List<string>();
            var valued = new List<ValuedPosition>();
            var invested = 0m;
            var current = 0m;

            foreach (var position in portfolio.Positions)
            {
                if (!assets.TryGetValue(position.AssetSymbol, out var asset))
                {
                    var message = $"Posição em '{position.AssetSymbol}' ignorada: ativo não encontrado no cadastro.";
                    warnings.Add(message);
                    _logger.LogWarning(
                        "Portfólio {PortfolioId}: posição órfã em {Symbol} excluída dos cálculos.",
                        portfolioId, position.AssetSymbol);
                    continue;
                }

                if (asset.CurrentPrice <= 0)
                    warnings.Add($"Ativo '{asset.Symbol}' possui preço atual não-positivo ({asset.CurrentPrice}); a posição foi valorada em zero.");

                if (position.AveragePrice <= 0)
                    warnings.Add($"Posição em '{asset.Symbol}' possui preço médio não-positivo; o retorno individual não pôde ser calculado.");

                var positionInvested = position.Quantity * position.AveragePrice;
                var positionCurrent = position.Quantity * Math.Max(asset.CurrentPrice, 0m);

                invested += positionInvested;
                current += positionCurrent;

                valued.Add(new ValuedPosition
                {
                    Position = position,
                    Asset = asset,
                    InvestedAmount = positionInvested,
                    CurrentValue = positionCurrent,
                    HasPriceHistory = asset.PriceHistory.Count >= 2
                });
            }
            var withWeights = AssignWeights(valued, current, warnings, portfolioId);
            var coverage = CalculateCoverage(withWeights, current);

            if (coverage < 1m)
            {
                var uncovered = withWeights.Where(p => !p.HasPriceHistory).Select(p => p.Symbol);
                warnings.Add(
                    $"Sem histórico de preços para {string.Join(", ", uncovered)}. " +
                    $"A volatilidade foi calculada sobre {FinancialMath.ToPercent(coverage)}% do valor do portfólio.");
            }

            // Premissa P3: divergência entre o capital declarado e o efetivamente alocado.
            if (Math.Abs(portfolio.TotalInvestment - invested) > 0.01m)
            {
                warnings.Add(
                    $"O campo 'totalInvestment' ({FinancialMath.ToMoney(portfolio.TotalInvestment)}) diverge do capital " +
                    $"alocado nas posições ({FinancialMath.ToMoney(invested)}). O retorno principal usa o capital alocado; " +
                    "'totalReturnOnInvestment' expõe a métrica sobre o valor declarado.");
            }

            // Premissa P2: o período é medido contra a data de referência do seed, nunca contra o relógio.
            var createdOn = DateOnly.FromDateTime(portfolio.CreatedAt);
            var periodInDays = market.AsOfDate.DayNumber - createdOn.DayNumber;

            if (periodInDays <= 0)
                warnings.Add("A data de criação do portfólio não é anterior à data de referência; o retorno anualizado não pôde ser calculado.");

            return new ValuedPortfolio
            {
                Portfolio = portfolio,
                Market = market,
                Positions = withWeights,
                InvestedAmount = invested,
                CurrentValue = current,
                VolatilityCoverage = coverage,
                PeriodInDays = periodInDays,
                Warnings = warnings
            };
        }
        private List<ValuedPosition> AssignWeights(
        List<ValuedPosition> positions, decimal currentValue, List<string> warnings, int portfolioId)
        {
            var targetSum = positions.Sum(p => p.Position.TargetAllocation);
            var needsNormalization = Math.Abs(targetSum - 1m) > AllocationTolerance;

            if (needsNormalization && targetSum > 0)
            {
                warnings.Add(
                    $"As alocações-alvo somam {FinancialMath.ToPercent(targetSum)}% e foram normalizadas proporcionalmente para 100%.");
                _logger.LogWarning(
                    "Portfólio {PortfolioId}: alocações-alvo somam {Sum:P2}; normalizadas para 100%.",
                    portfolioId, targetSum);
            }
            else if (targetSum <= 0)
            {
                warnings.Add("Nenhuma alocação-alvo válida foi encontrada; a análise de rebalanceamento não é aplicável.");
            }

            return positions.Select(p => p with
            {
                // Se o portfólio não tem valor de mercado, todos os pesos são zero — evita divisão por zero.
                Weight = currentValue > 0 ? p.CurrentValue / currentValue : 0m,
                TargetWeight = targetSum > 0 ? p.Position.TargetAllocation / targetSum : 0m
            }).ToList();
        }
        private static decimal CalculateCoverage(List<ValuedPosition> positions, decimal currentValue)
        {
            if (currentValue <= 0)
                return 0m;

            var covered = positions.Where(p => p.HasPriceHistory).Sum(p => p.CurrentValue);
            return covered / currentValue;
        }
    }
}
