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
    internal class RebalancingOptimizerTests
    {
        private static (Asset[] assets, Portfolio portfolio) DesbalanceadoFixture()
        {
            Asset[] assets =
            [
                TestData.Asset("PETR4", 35.50m, "Energy"),
            TestData.Asset("VALE3", 65.20m, "Mining"),
            TestData.Asset("ITUB4", 32.10m, "Financial"),
            TestData.Asset("MGLU3", 8.75m, "Retail")
            ];
            var portfolio = TestData.Portfolio(1, 50_000m, new(2024, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                TestData.Position("PETR4", 800, 32.00m, 0.25m),
                TestData.Position("VALE3", 200, 63.00m, 0.25m),
                TestData.Position("ITUB4", 400, 31.00m, 0.25m),
                TestData.Position("MGLU3", 300, 10.00m, 0.25m));

            return (assets, portfolio);
        }
        [Fact]
        public async Task OptimizeAsync_ComDesvioDentroDoLimite_NaoSugereTrade()
        {
            // Pesos 51% / 49% contra alvos de 50%
            // Rebalancear aqui custaria corretagem
            Asset[] assets = [TestData.Asset("AAAA", 51m, "S1"), TestData.Asset("BBBB", 49m, "S2")];
            var portfolio = TestData.Portfolio(1, 100m, new(2024, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                TestData.Position("AAAA", 100, 50m, 0.50m),
                TestData.Position("BBBB", 100, 50m, 0.50m));
            using var db = TestData.CreateContext(assets, [portfolio]);

            var result = await TestData.Rebalancing(db).OptimizeAsync(1);

            Assert.NotNull(result);
            Assert.False(result.NeedsRebalancing);
            Assert.All(result.CurrentAllocation, a => Assert.True(Math.Abs(a.Deviation) <= 2m));
        }
        [Fact]
        public async Task OptimizeAsync_ComPosicaoSobrealocada_SugereVenda()
        {
            var (assets, portfolio) = DesbalanceadoFixture();
            using var db = TestData.CreateContext(assets, [portfolio]);

            var result = await TestData.Rebalancing(db).OptimizeAsync(1);

            Assert.NotNull(result);
            Assert.True(result.NeedsRebalancing);

            var petr4 = result.SuggestedTrades.Single(t => t.Symbol == "PETR4");
            Assert.Equal("SELL", petr4.Action);
            // (28.400 − 14.226,25) / 35,50 = 399,26 → 399 ações
            Assert.Equal(399, petr4.Quantity);
            Assert.Equal(14_164.50m, petr4.EstimatedValue);
        }
        [Fact]
        public async Task OptimizeAsync_ComPosicaoSubalocada_SugereCompra()
        {
            var (assets, portfolio) = DesbalanceadoFixture();
            using var db = TestData.CreateContext(assets, [portfolio]);

            var result = await TestData.Rebalancing(db).OptimizeAsync(1);

            Assert.NotNull(result);

            var mglu3 = result.SuggestedTrades.Single(t => t.Symbol == "MGLU3");
            Assert.Equal("BUY", mglu3.Action);
            // (14.226,25 − 2.625) / 8,75 = 1.325,85 → 1.325 ações
            Assert.Equal(1325, mglu3.Quantity);
            Assert.Equal(11_593.75m, mglu3.EstimatedValue);
        }
    }
}
