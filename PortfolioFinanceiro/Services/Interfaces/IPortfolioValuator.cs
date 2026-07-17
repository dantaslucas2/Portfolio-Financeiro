using PortfolioFinanceiro.Models;

namespace PortfolioFinanceiro.Services.Interfaces
{
    public interface IPortfolioValuator
    {
        Task<ValuedPortfolio?> ValueAsync(int portfolioId, CancellationToken ct = default);
    }
}
