using PortfolioFinanceiro.Models.DTOs;
using PortfolioFinanceiro.Services.Interfaces;

namespace PortfolioFinanceiro.Services
{
    public class PerformanceCalculator : IPerformanceCalculator
    {
        private readonly IPortfolioValuator _valuator;
        private readonly ILogger<PerformanceCalculator> _logger;
        public PerformanceCalculator(IPortfolioValuator valuator, ILogger<PerformanceCalculator> logger)
        {
            _valuator = valuator;
            _logger = logger;
        }

        public async Task<PerformanceResponse> CalculateAsync()
        {
            throw new NotImplementedException();
        }

        public static PositionPerformance()
        {
            throw new NotImplementedException();
        }


    }
}
