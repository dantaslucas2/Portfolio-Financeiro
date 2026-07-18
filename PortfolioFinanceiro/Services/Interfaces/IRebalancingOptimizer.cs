using PortfolioFinanceiro.Models.DTOs;

namespace PortfolioFinanceiro.Services.Interfaces
{
    public interface IRebalancingOptimizer
    {
        Task<RebalancingResponse?> OptimizeAsync(int portfolioId, CancellationToken ct = default);
    }
}
