namespace PortfolioFinanceiro.Services
{
    public class FinancialMath
    {
        public const int TradingDaysPerYear = 252;

        private const int CalendarDaysPerYear = 365;

        public static decimal? SimpleReturn(decimal initialValue, decimal finalValue) =>
    initialValue > 0 ? (finalValue - initialValue) / initialValue : null;

        public static decimal? Annualize(decimal totalReturn, int periodInDays)
        {
            if (periodInDays <= 0)
                return null;

            var growth = 1d + (double)totalReturn;
            if (growth <= 0d)
                return null;

            var annualized = Math.Pow(growth, (double)CalendarDaysPerYear / periodInDays) - 1d;
            return double.IsFinite(annualized) ? (decimal)annualized : null;
        }

        public static IReadOnlyList<decimal> DailyReturns(IReadOnlyList<decimal> orderedPrices)
        {
            if (orderedPrices.Count < 2)
                return [];

            var returns = new List<decimal>(orderedPrices.Count - 1);
            for (var i = 1; i < orderedPrices.Count; i++)
            {
                var previous = orderedPrices[i - 1];
                if (previous <= 0)
                    continue;

                returns.Add(orderedPrices[i] / previous - 1m);
            }

            return returns;
        }

        public static decimal? SampleStandardDeviation(IReadOnlyList<decimal> values)
        {
            if (values.Count < 2)
                return null;

            var mean = values.Average();
            var sumSquares = values.Sum(v => (v - mean) * (v - mean));
            var variance = sumSquares / (values.Count - 1);

            if (variance <= 0)
                return 0m;

            return (decimal)Math.Sqrt((double)variance);
        }
        public static decimal AnnualizeVolatility(decimal dailyVolatility) =>
        dailyVolatility * (decimal)Math.Sqrt(TradingDaysPerYear);

        public static decimal? SharpeRatio(decimal? annualizedReturn, decimal? annualizedVolatility, decimal riskFreeRate)
        {
            if (annualizedReturn is null || annualizedVolatility is null or 0m)
                return null;

            return (annualizedReturn.Value - riskFreeRate) / annualizedVolatility.Value;
        }

        public static decimal HerfindahlIndex(IEnumerable<decimal> weights) =>
        weights.Sum(w => w * w);

        public static decimal ToPercent(decimal fraction) =>
        Math.Round(fraction * 100m, 2, MidpointRounding.AwayFromZero);

        public static decimal? ToPercent(decimal? fraction) =>
            fraction is null ? null : ToPercent(fraction.Value);

        public static decimal ToMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
