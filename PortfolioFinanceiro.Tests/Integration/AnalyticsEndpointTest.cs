using Microsoft.AspNetCore.Mvc.Testing;
using PortfolioFinanceiro.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace PortfolioFinanceiro.Tests.Integration
{
    /// <summary>
    /// Testes de ponta a ponta sobre os endpoints
    /// </summary>
    public class AnalyticsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly HttpClient _client;

        public AnalyticsEndpointTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient();

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task Performance_ParaOsTresPortfoliosDoSeed_RetornaOk(int id)
        {
            // Os três portfólios são acessíveis por 1, 2 e 3.
            var response = await _client.GetAsync($"/api/portfolios/{id}/performance");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PerformanceResponse>(Json);
            Assert.NotNull(body);
            Assert.True(body.CurrentValue > 0);
            Assert.NotEmpty(body.PositionsPerformance);
            Assert.Equal(new DateOnly(2024, 10, 06), body.AsOfDate);
        }

        [Fact]
        public async Task Performance_DoPortfolioConservador_RetornaValoresConferidosAMao()
        {
            // Alocado 76.800, atual 80.940, retorno 5,39%. Se o seed ou uma fórmula mudarem, será acusado error
            var body = await _client.GetFromJsonAsync<PerformanceResponse>("/api/portfolios/1/performance", Json);

            Assert.NotNull(body);
            Assert.Equal(100_000m, body.TotalInvestment);
            Assert.Equal(76_800m, body.InvestedAmount);
            Assert.Equal(80_940m, body.CurrentValue);
            Assert.Equal(5.39m, body.TotalReturn);
            Assert.Equal(5, body.PositionsPerformance.Count);
        }
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task RiskAnalysis_ParaOsTresPortfoliosDoSeed_RetornaOk(int id)
        {
            var body = await _client.GetFromJsonAsync<RiskAnalysisResponse>($"/api/portfolios/{id}/risk-analysis", Json);

            Assert.NotNull(body);
            Assert.Contains(body.OverallRisk, new[] { "Low", "Medium", "High" });
            Assert.Equal(12m, body.RiskFreeRate); // Selic do seed (0.12)
            Assert.NotEmpty(body.SectorDiversification);
            Assert.Equal(100m, body.SectorDiversification.Sum(s => s.Percentage), 1);
        }
        [Theory]
        [InlineData("/api/portfolios/999/performance")]
        [InlineData("/api/portfolios/999/risk-analysis")]
        [InlineData("/api/portfolios/999/rebalancing")]
        public async Task Endpoints_ComPortfolioInexistente_Retornam404ComProblemDetails(string url)
        {
            // retorne 404 para os portifólios não existentes".
            var response = await _client.GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Portfólio não encontrado", problem.GetProperty("title").GetString());
            Assert.Equal(404, problem.GetProperty("status").GetInt32());
        }
        [Fact]
        public async Task Endpoints_ComIdNaoNumerico_Retornam404PelaRestricaoDeRota()
        {
            // rejeita a rota antes de chegar ao controller.
            var response = await _client.GetAsync("/api/portfolios/abc/performance");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
