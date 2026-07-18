using PortfolioFinanceiro.Services;
using Xunit;

namespace PortfolioFinanceiro.Tests
{
    public class FinancialMathTests
    {
        [Theory]
        [InlineData(100, 110, 0.10)]
        public void CalculatesReturnOnInitialCapital(decimal initial, decimal final, decimal expected)
        {
            var result = FinancialMath.SimpleReturn(initial, final);

            Assert.NotNull(result);
            Assert.Equal(expected, result!.Value);
        }
    }
}
