using PortfolioFinanceiro.Services;
using Xunit;

namespace PortfolioFinanceiro.Tests.Unit
{
    public class FinancialMathTests
    {
        [Theory]
        [InlineData(100, 110, 0.10)] //gain
        [InlineData(100, 90, -0.10)] //loss
        [InlineData(100, 100, 0.00)] //stable
        public void CalculatesReturnOnInitialCapital(decimal initial, decimal final, decimal expected)
        {
            var result = FinancialMath.SimpleReturn(initial, final);

            Assert.NotNull(result);
            Assert.Equal(expected, result!.Value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public void SimpleReturn_ComCapitalInicialNaoPositivo_RetornaNull(decimal initial)
        {
            // divisão por zero não pode lançar nem retornar 0,
            Assert.Null(FinancialMath.SimpleReturn(initial, 100m));
        }

        [Fact]
        public void Annualize_ComPeriodoDeUmAno_PreservaORetorno()
        {
            var result = FinancialMath.Annualize(0.10m, 365);

            Assert.NotNull(result);
            Assert.Equal(0.10m, result.Value, 6);
        }
        [Fact]
        public void DailyReturns_CalculaVariacaoEntreDiasConsecutivos()
        {
            var result = FinancialMath.DailyReturns([100m, 110m, 99m]);

            Assert.Equal(2, result.Count);
            Assert.Equal(0.10m, result[0], 6);
            Assert.Equal(-0.10m, result[1], 6);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void DailyReturns_ComMenosDeDoisPrecos_RetornaVazio(int count)
        {
            var prices = Enumerable.Repeat(10m, count).ToList();

            Assert.Empty(FinancialMath.DailyReturns(prices));
        }
        [Fact]
        public void SampleStandardDeviation_UsaDenominadorNMenos1()
        {
            var result = FinancialMath.SampleStandardDeviation([1m, 2m, 3m, 4m]);

            Assert.NotNull(result);
            Assert.Equal(1.2910m, result.Value, 4);
        }
        [Theory]
        [InlineData(0.0850, 8.50)]
        [InlineData(0.123456, 12.35)]  // arredonda a 2 casas
        [InlineData(-0.0234, -2.34)]
        public void ToPercent_ConverteFracaoEmPontosPercentuais(decimal fraction, decimal expected)
        {
            Assert.Equal(expected, FinancialMath.ToPercent(fraction));
        }
    }
}
