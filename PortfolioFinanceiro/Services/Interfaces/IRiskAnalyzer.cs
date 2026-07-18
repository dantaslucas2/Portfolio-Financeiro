using PortfolioFinanceiro.Models.DTOs;

namespace PortfolioFinanceiro.Services.Interfaces
{
    public interface IRiskAnalyzer
    {
        Task<RiskAnalysisResponse?> AnalyzeAsync(int portfolioId, CancellationToken ct = default);
    }
}
