using Microsoft.AspNetCore.Http.HttpResults;
using PortfolioAnalytics.Tests;
using PortfolioFinanceiro.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PortfolioFinanceiro.Tests.Unit.ServicesTests
{
    public class RiskAnalyzerTests
    {
        [Fact]
        public async Task AnalyzeAsync_ComPosicaoAcimaDe25Porcento_ClassificaRiscoAlto()
        {

            Asset[] assets =
            [
                TestData.Asset("VALE3", 65.20m, "Mining"),
            TestData.Asset("PETR4", 35.50m, "Energy"),
            TestData.Asset("ITUB4", 32.10m, "Financial")
            ];
            var portfolio = TestData.Portfolio(1, 80_000m, new(2024, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                TestData.Position("VALE3", 1000, 60.00m, 0.70m),
                TestData.Position("PETR4", 200, 35.00m, 0.20m),
                TestData.Position("ITUB4", 100, 32.00m, 0.10m));
            using var db = TestData.CreateContext(assets, [portfolio]);

            var result = await TestData.Risk(db).AnalyzeAsync(1);

            Assert.NotNull(result);
            Assert.Equal("High", result.OverallRisk);
            Assert.Equal("VALE3", result.ConcentrationRisk.LargestPosition.Symbol);
            Assert.Equal(86.35m, result.ConcentrationRisk.LargestPosition.Percentage, 1);
        }
        [Fact]
        public async Task AnalyzeAsync_ComSetorAcimaDe40Porcento_ClassificaRiscoAlto()
        {
            // BBDC4 + ITUB4 concentram 43,3% no setor Financial. 
            Asset[] assets =
            [
                TestData.Asset("BBDC4", 15.80m, "Financial"),
            TestData.Asset("ITUB4", 32.10m, "Financial"),
            TestData.Asset("PETR4", 35.50m, "Energy"),
            TestData.Asset("VALE3", 65.20m, "Mining"),
            TestData.Asset("WEGE3", 42.85m, "Industrial")
            ];
            var portfolio = TestData.Portfolio(1, 100_000m, new(2024, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                TestData.Position("PETR4", 500, 30.00m, 0.20m),
                TestData.Position("VALE3", 300, 60.00m, 0.25m),
                TestData.Position("BBDC4", 1000, 18.00m, 0.20m),
                TestData.Position("ITUB4", 600, 28.00m, 0.25m),
                TestData.Position("WEGE3", 200, 45.00m, 0.10m));
            using var db = TestData.CreateContext(assets, [portfolio]);

            var result = await TestData.Risk(db).AnalyzeAsync(1);

            Assert.NotNull(result);
            var financial = result.SectorDiversification.Single(s => s.Sector == "Financial");
            Assert.Equal(43.32m, financial.Percentage, 1);
            Assert.Equal("High", financial.Risk);
            Assert.Equal("High", result.OverallRisk);
            Assert.All(result.SectorDiversification.Where(s => s.Sector != "Financial"),
                s => Assert.NotEqual("High", s.Risk));
        }
        [Fact]
        public async Task AnalyzeAsync_ComTudoDentroDosLimites_ClassificaRiscoBaixo()
        {
            // 8 posições de 12,5% em setores distintos: abaixo de 15% por posição e 25% por setor.
            var assets = Enumerable.Range(1, 8)
                .Select(i => TestData.Asset($"AAA{i}", 10m, $"Sector{i}"))
                .ToArray();
            var positions = Enumerable.Range(1, 8)
                .Select(i => TestData.Position($"AAA{i}", 100, 10m, 0.125m))
                .ToArray();
            var portfolio = TestData.Portfolio(1, 8_000m, new(2024, 01, 15, 09, 00, 00, DateTimeKind.Utc), positions);
            using var db = TestData.CreateContext(assets, [portfolio]);

            var result = await TestData.Risk(db).AnalyzeAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Low", result.OverallRisk);
            Assert.Contains(result.Recommendations, r => r.Contains("Nenhum risco relevante"));
        }
    }
}
