using Microsoft.AspNetCore.Http.HttpResults;
using PortfolioAnalytics.Tests;
using PortfolioFinanceiro.Models;
using Xunit;

namespace PortfolioFinanceiro.Tests.Unit.ServicesTests;

public class PerformanceCalculatorTests
{
    /// <summary>
    /// Testes sobre os calculos da service PerformanceCalculator
    /// </summary>
    private static (Asset[] assets, Portfolio portfolio) ConservadorFixture()
    {
        Asset[] assets =
        [
            TestData.Asset("PETR4", 35.50m, "Energy", 34.00m, 35.00m, 35.50m),
            TestData.Asset("VALE3", 65.20m, "Mining", 64.00m, 65.00m, 65.20m),
            TestData.Asset("BBDC4", 15.80m, "Financial", 16.00m, 15.90m, 15.80m),
            TestData.Asset("ITUB4", 32.10m, "Financial", 31.50m, 32.00m, 32.10m),
            TestData.Asset("WEGE3", 42.85m, "Industrial") // sem histórico
        ];

        var portfolio = TestData.Portfolio(1, 100_000m, new(2024, 01, 15, 09, 00, 00, DateTimeKind.Utc),
            TestData.Position("PETR4", 500, 30.00m, 0.20m),
            TestData.Position("VALE3", 300, 60.00m, 0.25m),
            TestData.Position("BBDC4", 1000, 18.00m, 0.20m),
            TestData.Position("ITUB4", 600, 28.00m, 0.25m),
            TestData.Position("WEGE3", 200, 45.00m, 0.10m));

        return (assets, portfolio);
    }
    [Fact]
    public async Task CalculateAsync_CalculaRetornoTotalSobreCapitalAlocado()
    {
        /// Conferencia do valores conferidos manualmente
        /// alocado = 500×30 + 300×60 + 1000×18 + 600×28 + 200×45 = 76.800
        /// atual   = 500×35,50 + 300×65,20 + 1000×15,80 + 600×32,10 + 200×42,85 = 80.940
        /// retorno = 4.140 / 76.800 = 5,390625%
        var (assets, portfolio) = ConservadorFixture();
        using var db = TestData.CreateContext(assets, [portfolio]);

        var result = await TestData.Performance(db).CalculateAsync(1);

        Assert.NotNull(result);
        Assert.Equal(76_800m, result.InvestedAmount);
        Assert.Equal(80_940m, result.CurrentValue);
        Assert.Equal(5.39m, result.TotalReturn);
        Assert.Equal(4_140m, result.TotalReturnAmount);
    }
    [Fact]
    public async Task CalculateAsync_SemHistoricoDePrecos_RetornaVolatilidadeNull()
    {
        Asset[] assets = [TestData.Asset("WEGE3", 42.85m, "Industrial")];
        var portfolio = TestData.Portfolio(1, 9_000m, new(2024, 01, 15, 09, 00, 00, DateTimeKind.Utc), TestData.Position("WEGE3", 200, 45.00m, 1.00m));
        using var db = TestData.CreateContext(assets, [portfolio]);

        var result = await TestData.Performance(db).CalculateAsync(1);

        Assert.NotNull(result);
        Assert.Null(result.Volatility);
        Assert.Equal(0m, result.VolatilityCoverage);
        Assert.All(result.PositionsPerformance, p => Assert.Null(p.Volatility));
    }
    [Fact]
    public async Task CalculateAsync_ComPortfolioInexistente_RetornaNull()
    {
        // ausência é null, não exceção.
        var (assets, portfolio) = ConservadorFixture();
        using var db = TestData.CreateContext(assets, [portfolio]);

        Assert.Null(await TestData.Performance(db).CalculateAsync(999));
    }
    [Fact]
    public async Task CalculateAsync_AnualizaRetornoComBaseNoPeriodoDecorrido()
    {
        // 5,390625% em 265 dias → (1.05390625)^(365/265) - 1 = 7,47%.
        var (assets, portfolio) = ConservadorFixture();
        using var db = TestData.CreateContext(assets, [portfolio]);

        var result = await TestData.Performance(db).CalculateAsync(1);

        Assert.NotNull(result);
        Assert.NotNull(result.AnnualizedReturn);
        Assert.Equal(7.47m, result.AnnualizedReturn.Value, 1);
    }
    [Fact]
    public async Task CalculateAsync_PesosDasPosicoesSomamCem()
    {
        var (assets, portfolio) = ConservadorFixture();
        using var db = TestData.CreateContext(assets, [portfolio]);

        var result = await TestData.Performance(db).CalculateAsync(1);

        Assert.NotNull(result);
        Assert.Equal(100m, result.PositionsPerformance.Sum(p => p.Weight), 1);
    }
}
