using PortfolioFinanceiro.Models.DTOs;

namespace PortfolioFinanceiro.Services.Interfaces
{
    public interface IPerformanceCalculator
    {
        Task<PerformanceResponse?> CalculateAsync(int portfolioId, CancellationToken ct = default);
    }
}
