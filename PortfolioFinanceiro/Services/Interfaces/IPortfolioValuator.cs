using PortfolioFinanceiro.Models;

namespace PortfolioFinanceiro.Services.Interfaces
{
    interface IPortfolioValuator
    {
        Task<ValuedPortfolio?> ValueAsync(int portfolioId, CancellationToken ct = default);
    }
}
