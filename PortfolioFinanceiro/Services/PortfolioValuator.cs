using PortfolioFinanceiro.Models;
using PortfolioFinanceiro.Services.Interfaces;

namespace PortfolioFinanceiro.Services
{
    public class PortfolioValuator : IPortfolioValuator
    {
        public Task<ValuedPortfolio?> ValueAsync(int portfolioId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
