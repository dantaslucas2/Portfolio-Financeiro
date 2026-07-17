using PortfolioFinanceiro.Models;
using PortfolioFinanceiro.Models.DTOs;
using PortfolioFinanceiro.Services.Interfaces;

namespace PortfolioFinanceiro.Services
{
    internal sealed class PerformanceCalculator : IPerformanceCalculator
    {
        private readonly IPortfolioValuator _valuator;
        private readonly ILogger<PerformanceCalculator> _logger;
        public PerformanceCalculator(IPortfolioValuator valuator, ILogger<PerformanceCalculator> logger)
        {
            _valuator = valuator;
            _logger = logger;
        }

        public async Task<PerformanceResponse> CalculateAsync(int portfolioId, CancellationToken ct = default)
        {
            var valued = await _valuator.ValueAsync(portfolioId, ct);
            if (valued is null)
                return null;

            var totalReturn = valued.TotalReturn;
            var annualized = totalReturn is null ? null : FinancialMath.Annualize(totalReturn.Value, valued.PeriodInDays);
            var volatility = CalculatePortfolioVolatility(valued);

            var returnOnInvestment = FinancialMath.SimpleReturn(
            valued.Portfolio.TotalInvestment,
            valued.CurrentValue + Math.Max(valued.Portfolio.TotalInvestment - valued.InvestedAmount, 0m));

            _logger.LogDebug(
                "Portfólio {PortfolioId}: alocado {Invested}, atual {Current}, retorno {Return:P4}, anualizado {Annualized:P4}, vol {Volatility:P4} (cobertura {Coverage:P2}, {Days} dias)",
                portfolioId, valued.InvestedAmount, valued.CurrentValue, totalReturn, annualized, volatility, valued.VolatilityCoverage, valued.PeriodInDays);

            return new PerformanceResponse
            {
                TotalInvestment = FinancialMath.ToMoney(valued.Portfolio.TotalInvestment),
                InvestedAmount = FinancialMath.ToMoney(valued.InvestedAmount),
                CurrentValue = FinancialMath.ToMoney(valued.CurrentValue),
                TotalReturn = FinancialMath.ToPercent(totalReturn) ?? 0m,
                TotalReturnAmount = FinancialMath.ToMoney(valued.CurrentValue - valued.InvestedAmount),
                TotalReturnOnInvestment = FinancialMath.ToPercent(returnOnInvestment) ?? 0m,
                AnnualizedReturn = FinancialMath.ToPercent(annualized),
                Volatility = FinancialMath.ToPercent(volatility),
                VolatilityCoverage = Math.Round(valued.VolatilityCoverage, 4),
                AsOfDate = valued.Market.AsOfDate,
                PeriodInDays = valued.PeriodInDays,
                PositionsPerformance = valued.Positions.Select(MapPosition).ToList(),
                Warnings = valued.Warnings
            };
        }

        public static PositionPerformance MapPosition(ValuedPosition p) => new()
        {
            Symbol = p.Symbol,
            Quantity = p.Position.Quantity,
            AveragePrice = FinancialMath.ToMoney(p.Position.AveragePrice),
            CurrentPrice = FinancialMath.ToMoney(p.Asset.CurrentPrice),
            InvestedAmount = FinancialMath.ToMoney(p.InvestedAmount),
            CurrentValue = FinancialMath.ToMoney(p.CurrentValue),
            Return = FinancialMath.ToPercent(p.Return),
            Weight = FinancialMath.ToPercent(p.Weight),
            Volatility = FinancialMath.ToPercent(CalculateAssetVolatility(p.Asset))
        };
        internal static decimal? CalculateAssetVolatility(Asset asset)
        {
            var prices = asset.PriceHistory.OrderBy(h => h.Date).Select(h => h.Price).ToList();
            var returns = FinancialMath.DailyReturns(prices);
            var daily = FinancialMath.SampleStandardDeviation(returns);

            return daily is null ? null : FinancialMath.AnnualizeVolatility(daily.Value);
        }
        internal static decimal? CalculatePortfolioVolatility(ValuedPortfolio valued)
        {
            var covered = valued.Positions.Where(p => p.HasPriceHistory).ToList();
            if (covered.Count == 0)
                return null;

            var seriesBySymbol = covered.ToDictionary(
                p => p.Symbol,
                p => p.Asset.PriceHistory.ToDictionary(h => h.Date, h => h.Price));

            var commonDates = seriesBySymbol.Values
                .Select(s => (IEnumerable<DateOnly>)s.Keys)
                .Aggregate((a, b) => a.Intersect(b))
                .OrderBy(d => d)
                .ToList();

            if (commonDates.Count < 2)
                return null;

            var portfolioValues = commonDates
                .Select(date => covered.Sum(p => p.Position.Quantity * seriesBySymbol[p.Symbol][date]))
                .ToList();

            var returns = FinancialMath.DailyReturns(portfolioValues);
            var daily = FinancialMath.SampleStandardDeviation(returns);

            return daily is null ? null : FinancialMath.AnnualizeVolatility(daily.Value);
        }

    }
}
