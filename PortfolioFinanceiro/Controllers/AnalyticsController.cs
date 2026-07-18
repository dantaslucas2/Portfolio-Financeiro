using Microsoft.AspNetCore.Mvc;
using PortfolioFinanceiro.Models.DTOs;
using PortfolioFinanceiro.Services.Interfaces;

namespace PortfolioFinanceiro.Controllers
{
    [ApiController]
    [Route("api/portfolios")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IPerformanceCalculator _performance;
        private readonly IRiskAnalyzer _risk;
        private readonly IRebalancingOptimizer _rebalancing;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            IPerformanceCalculator performance,
            IRiskAnalyzer risk,
            IRebalancingOptimizer rebalancing,
            ILogger<AnalyticsController> logger)
        {
            _performance = performance;
            _risk = risk;
            _rebalancing = rebalancing;
            _logger = logger;
        }

        [HttpGet("{id:int}/performance")]
        public async Task<ActionResult<PerformanceResponse>> GetPerformance(int id, CancellationToken ct)
        {
            if (id <= 0)
                return InvalidId(id);

            var result = await _performance.CalculateAsync(id, ct);
            if (result is null)
                return PortfolioNotFound(id);

            _logger.LogInformation(
                "Performance calculada para portfólio {PortfolioId}: retorno {TotalReturn}% sobre {InvestedAmount:C}, cobertura de volatilidade {Coverage:P0}",
                id, result.TotalReturn, result.InvestedAmount, result.VolatilityCoverage);

            return Ok(result);
        }

        [HttpGet("{id:int}/risk-analysis")]
        public async Task<ActionResult<RiskAnalysisResponse>> GetRiskAnalysis(int id, CancellationToken ct)
        {
            if (id <= 0)
                return InvalidId(id);

            var result = await _risk.AnalyzeAsync(id, ct);
            if (result is null)
                return PortfolioNotFound(id);

            _logger.LogInformation(
                "Risco analisado para portfólio {PortfolioId}: nível {OverallRisk}, maior posição {Symbol} com {Percentage}%, Sharpe {Sharpe}",
                id, result.OverallRisk, result.ConcentrationRisk.LargestPosition.Symbol,
                result.ConcentrationRisk.LargestPosition.Percentage, result.SharpeRatio);

            return Ok(result);
        }

        [HttpGet("{id:int}/rebalancing")]
        public async Task<ActionResult<RebalancingResponse>> GetRebalancing(int id, CancellationToken ct)
        {
            if (id <= 0)
                return InvalidId(id);

            var result = await _rebalancing.OptimizeAsync(id, ct);
            if (result is null)
                return PortfolioNotFound(id);

            _logger.LogInformation(
                "Rebalanceamento avaliado para portfólio {PortfolioId}: necessário={NeedsRebalancing}, {TradeCount} trades, custo total {Cost:C}",
                id, result.NeedsRebalancing, result.SuggestedTrades.Count, result.TotalTransactionCost);

            return Ok(result);
        }
        private ActionResult InvalidId(int id)
        {
            _logger.LogWarning("Requisição com identificador inválido: {PortfolioId}", id);
            return Problem(
                title: "Identificador inválido",
                detail: $"O identificador do portfólio deve ser um inteiro positivo. Recebido: {id}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        private ActionResult PortfolioNotFound(int id)
        {
            _logger.LogWarning("Portfólio {PortfolioId} não encontrado", id);
            return Problem(
                title: "Portfólio não encontrado",
                detail: $"Não existe portfólio com o identificador {id}. Identificadores disponíveis: 1, 2 e 3.",
                statusCode: StatusCodes.Status404NotFound);
        }
    }
}
