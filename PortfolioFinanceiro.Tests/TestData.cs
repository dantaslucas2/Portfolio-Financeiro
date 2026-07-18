using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioFinanceiro.Data;
using PortfolioFinanceiro.Models;
using PortfolioFinanceiro.Services;

namespace PortfolioAnalytics.Tests;


internal static class TestData
{
    public static readonly DateOnly AsOf = new(2024, 10, 06);

    public static DataContext CreateContext(
        IEnumerable<Asset> assets,
        IEnumerable<Portfolio> portfolios,
        decimal selicRate = 0.12m,
        DateOnly? asOf = null)
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase($"tests-{Guid.NewGuid()}")
            .Options;

        var db = new DataContext(options);
        db.Assets.AddRange(assets);
        db.Portfolios.AddRange(portfolios);
        db.MarketSnapshots.Add(new MarketSnapshot
        {
            Id = 1,
            SelicRate = selicRate,
            AsOfDate = asOf ?? AsOf
        });
        db.SaveChanges();

        return db;
    }

    public static PortfolioValuator Valuator(DataContext db) =>
        new(db, NullLogger<PortfolioValuator>.Instance);

    public static PerformanceCalculator Performance(DataContext db) =>
        new(Valuator(db), NullLogger<PerformanceCalculator>.Instance);

    public static RiskAnalyzer Risk(DataContext db) =>
        new(Valuator(db), NullLogger<RiskAnalyzer>.Instance);

    public static RebalancingOptimizer Rebalancing(DataContext db) =>
        new(Valuator(db), NullLogger<RebalancingOptimizer>.Instance);

    public static Asset Asset(
        string symbol,
        decimal currentPrice,
        string sector = "Generic",
        params decimal[] prices) => new()
        {
            Symbol = symbol,
            Name = $"{symbol} S.A.",
            Type = "Stock",
            Sector = sector,
            CurrentPrice = currentPrice,
            LastUpdated = AsOf.ToDateTime(TimeOnly.MinValue),
            PriceHistory = prices
                .Select((price, i) => new PricePoint
                {
                    AssetSymbol = symbol,
                    Date = AsOf.AddDays(i - prices.Length + 1),
                    Price = price
                })
                .ToList()
        };

    public static Portfolio Portfolio(
        int id,
        decimal totalInvestment,
        DateTime createdAt,
        params Position[] positions) => new()
        {
            Id = id,
            Name = $"Portfólio {id}",
            UserId = $"user-{id:000}",
            TotalInvestment = totalInvestment,
            CreatedAt = createdAt,
            Positions = positions.ToList()
        };

    public static Position Position(
        string symbol,
        decimal quantity,
        decimal averagePrice,
        decimal targetAllocation) => new()
        {
            AssetSymbol = symbol,
            Quantity = quantity,
            AveragePrice = averagePrice,
            TargetAllocation = targetAllocation
        };
}
