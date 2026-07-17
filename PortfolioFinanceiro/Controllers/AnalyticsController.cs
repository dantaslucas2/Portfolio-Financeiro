using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using PortfolioFinanceiro.Models.DTOs;

namespace PortfolioFinanceiro.Controllers
{
    [ApiController]
    [Route("api/portfolios")]
    public class AnalyticsController : ControllerBase
    {
        [HttpGet("{id:int}/performance")]
        public async Task<ActionResult<PerformanceResponse>> GetPerformance()
        {
            throw new System.NotImplementedException();
        }

        [HttpGet("{id:int}/risk-analysis")]
        public async Task<ActionResult<RiskAnalysisResponse>> GetRiskAnalysis()
        {
            throw new System.NotImplementedException();
        }

        [HttpGet("{id:int}/rebalancing")]
        public async Task<ActionResult<RebalancingResponse>> GetRebalancing()
        {
            throw new System.NotImplementedException();
        }
    }
}
