using PortfolioFinanceiro.Models;
using PortfolioFinanceiro.Models.DTOs;
using PortfolioFinanceiro.Services.Interfaces;

namespace PortfolioFinanceiro.Services
{
    internal sealed class RebalancingOptimizer : IRebalancingOptimizer
    {
        private readonly IPortfolioValuator _valuator;
        private readonly ILogger<RebalancingOptimizer> _logger;

        private const decimal DeviationThreshold = 0.02m;
        private const decimal MinimumTradeValue = 100m;//Minimal value for trade
        private const decimal TransactionCostRate = 0.003m;// cost per transaction

        public RebalancingOptimizer(IPortfolioValuator valuator, ILogger<RebalancingOptimizer> logger)
        {
            _valuator = valuator;
            _logger = logger;
        }
        public async Task<RebalancingResponse?> OptimizeAsync(int portfolioId, CancellationToken ct = default)
        {
            var valued = await _valuator.ValueAsync(portfolioId, ct);
            if (valued is null)
                return null;

            var warnings = valued.Warnings.ToList();
            var allocations = valued.Positions
                .Select(p => new AllocationDeviation
                {
                    Symbol = p.Symbol,
                    CurrentWeight = FinancialMath.ToPercent(p.Weight),
                    TargetWeight = FinancialMath.ToPercent(p.TargetWeight),
                    Deviation = FinancialMath.ToPercent(p.Weight - p.TargetWeight)
                })
                .OrderByDescending(a => Math.Abs(a.Deviation))
                .ToList();

            var trades = BuildTrades(valued, warnings);
            var improvement = CalculateImprovement(valued, trades);

            _logger.LogDebug(
                "Portfólio {PortfolioId}: {Count} trades sugeridos, desvio agregado {Before} p.p. → {After} p.p., custo {Cost}",
                portfolioId, trades.Count, improvement.TotalDeviationBefore, improvement.TotalDeviationAfter,
                trades.Sum(t => t.TransactionCost));

            return new RebalancingResponse
            {
                NeedsRebalancing = trades.Count > 0,
                CurrentValue = FinancialMath.ToMoney(valued.CurrentValue),
                DeviationThreshold = FinancialMath.ToPercent(DeviationThreshold),
                MinimumTradeValue = MinimumTradeValue,
                TransactionCostRate = FinancialMath.ToPercent(TransactionCostRate),
                CurrentAllocation = allocations,
                SuggestedTrades = trades,
                TotalTransactionCost = FinancialMath.ToMoney(trades.Sum(t => t.TransactionCost)),
                ExpectedImprovement = improvement,
                Warnings = warnings
            };
        }
        private static List<SuggestedTrade> BuildTrades(ValuedPortfolio valued, List<string> warnings)
        {
            if (valued.CurrentValue <= 0)
            {
                warnings.Add("O portfólio não possui valor de mercado; o rebalanceamento não é aplicável.");
                return [];
            }

            var trades = new List<SuggestedTrade>();

            foreach (var position in valued.Positions.OrderByDescending(p => Math.Abs(p.Weight - p.TargetWeight)))
            {
                var deviation = position.Weight - position.TargetWeight;

                if (Math.Abs(deviation) <= DeviationThreshold)
                    continue;

                var price = position.Asset.CurrentPrice;
                if (price <= 0)
                {
                    warnings.Add($"Trade em '{position.Symbol}' não pôde ser dimensionado: preço atual não-positivo.");
                    continue;
                }

                var targetValue = position.TargetWeight * valued.CurrentValue;
                var delta = targetValue - position.CurrentValue; 

                var quantity = (int)Math.Truncate(Math.Abs(delta) / price);
                if (quantity <= 0)
                    continue;

                var isSell = delta < 0;

                if (isSell)
                    quantity = (int)Math.Min(quantity, Math.Truncate(position.Position.Quantity));

                if (quantity <= 0)
                    continue;

                var tradeValue = quantity * price;
                if (tradeValue < MinimumTradeValue)
                    continue;

                var signedQuantity = isSell ? -quantity : quantity;
                var resultingValue = position.CurrentValue + signedQuantity * price;

                trades.Add(new SuggestedTrade
                {
                    Symbol = position.Symbol,
                    Action = isSell ? "SELL" : "BUY",
                    Quantity = quantity,
                    EstimatedValue = FinancialMath.ToMoney(tradeValue),
                    TransactionCost = FinancialMath.ToMoney(tradeValue * TransactionCostRate),
                    ResultingWeight = FinancialMath.ToPercent(resultingValue / valued.CurrentValue),
                    Reason = isSell
                        ? $"Reduzir de {FinancialMath.ToPercent(position.Weight)}% para {FinancialMath.ToPercent(position.TargetWeight)}%"
                        : $"Aumentar de {FinancialMath.ToPercent(position.Weight)}% para {FinancialMath.ToPercent(position.TargetWeight)}%"
                });
            }

            return trades;
        }
        private static ExpectedImprovement CalculateImprovement(ValuedPortfolio valued, List<SuggestedTrade> trades)
        {
            var before = valued.Positions.Sum(p => Math.Abs(p.Weight - p.TargetWeight));

            var bySymbol = trades.ToDictionary(t => t.Symbol);
            var after = valued.Positions.Sum(p =>
            {
                var weight = bySymbol.TryGetValue(p.Symbol, out var trade)
                    ? trade.ResultingWeight / 100m
                    : p.Weight;

                return Math.Abs(weight - p.TargetWeight);
            });

            var reduction = before > 0 ? (before - after) / before : 0m;

            var summary = trades.Count == 0
                ? "Nenhum ajuste necessário: todos os desvios estão dentro do limite de 2 p.p."
                : $"Redução de {FinancialMath.ToPercent(reduction)}% no desvio agregado " +
                  $"({FinancialMath.ToPercent(before)} p.p. → {FinancialMath.ToPercent(after)} p.p.) " +
                  $"com {trades.Count} operação(ões) e custo total de " +
                  $"{FinancialMath.ToMoney(trades.Sum(t => t.TransactionCost)):N2} " +
                  $"({FinancialMath.ToPercent(trades.Sum(t => t.TransactionCost) / valued.CurrentValue)}% do portfólio).";

            return new ExpectedImprovement
            {
                TotalDeviationBefore = FinancialMath.ToPercent(before),
                TotalDeviationAfter = FinancialMath.ToPercent(after),
                DeviationReduction = FinancialMath.ToPercent(reduction),
                Summary = summary
            };
        }
    }
}
