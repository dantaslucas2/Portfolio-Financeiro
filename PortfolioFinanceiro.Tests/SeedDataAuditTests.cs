using System.Text.Json;
using Xunit;

namespace PortfolioAnalytics.Tests;

public class SeedDataAuditTests
{
    private static readonly JsonDocument Seed = LoadSeed();

    private static JsonDocument LoadSeed()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static Dictionary<string, decimal> CurrentPrices() =>
        Seed.RootElement.GetProperty("assets")
            .EnumerateArray()
            .ToDictionary(
                a => a.GetProperty("symbol").GetString()!,
                a => a.GetProperty("currentPrice").GetDecimal());

    private static JsonElement Scenario(string name) =>
        Seed.RootElement.GetProperty("testScenarios")
            .EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == name);

    private static decimal CurrentValueOf(JsonElement scenario)
    {
        var prices = CurrentPrices();
        return scenario.GetProperty("portfolio").GetProperty("positions")
            .EnumerateArray()
            .Sum(p => p.GetProperty("quantity").GetDecimal() * prices[p.GetProperty("assetSymbol").GetString()!]);
    }

    private static decimal CostBasisOf(JsonElement scenario) =>
        scenario.GetProperty("portfolio").GetProperty("positions")
            .EnumerateArray()
            .Sum(p => p.GetProperty("quantity").GetDecimal() * p.GetProperty("averagePrice").GetDecimal());

    // ── Portfólios principais ────────────────────────────────────

    [Fact]
    public void Portfolios_NaoPossuemIdentificador()
    {
        // A rota é /api/portfolios/{id}, mas nenhum portfólio traz um id. Daí a premissa P1:
        // ids sequenciais 1..N atribuídos na ordem do arquivo durante a carga.
        var portfolios = Seed.RootElement.GetProperty("portfolios").EnumerateArray().ToList();

        Assert.All(portfolios, p => Assert.False(p.TryGetProperty("id", out _)));
        Assert.Equal(3, portfolios.Count);
    }

    [Fact]
    public void Portfolios_TotalInvestmentDivergeDoCapitalAlocadoNasPosicoes()
    {
        // Conservador: declara 100.000, aloca 76.800 (diferença de 23.200)
        // Crescimento: declara 250.000, aloca 158.620 (diferença de 91.380)
        // Dividendos:  declara 150.000, aloca 97.400  (diferença de 52.600)
        //
        // Não é arredondamento: a diferença chega a 37% do capital declarado. Usar o valor declarado
        // como base do retorno trataria dezenas de milhares de reais como capital de risco sem nenhuma
        // posição correspondente. Daí a premissa P3 — retorno principal sobre o capital alocado,
        // com 'totalReturnOnInvestment' exposto em paralelo.
        var divergencias = Seed.RootElement.GetProperty("portfolios")
            .EnumerateArray()
            .Select(p => new
            {
                Nome = p.GetProperty("name").GetString()!,
                Declarado = p.GetProperty("totalInvestment").GetDecimal(),
                Alocado = p.GetProperty("positions").EnumerateArray()
                    .Sum(pos => pos.GetProperty("quantity").GetDecimal() * pos.GetProperty("averagePrice").GetDecimal())
            })
            .ToList();

        Assert.All(divergencias, d => Assert.True(
            d.Declarado > d.Alocado,
            $"{d.Nome}: declarado {d.Declarado}, alocado {d.Alocado}"));

        var conservador = divergencias.Single(d => d.Nome.Contains("Conservador"));
        Assert.Equal(100_000m, conservador.Declarado);
        Assert.Equal(76_800m, conservador.Alocado);
    }

    [Fact]
    public void Portfolios_AlocacoesAlvoSomamCemPorCento()
    {
        // Ao contrário do que o FAQ do enunciado sugere, os três portfólios principais estão corretos
        // neste ponto. O tratamento de normalização existe mesmo assim — é exigido pelo FAQ e coberto
        // por teste próprio em RebalancingOptimizerTests.
        var somas = Seed.RootElement.GetProperty("portfolios")
            .EnumerateArray()
            .Select(p => p.GetProperty("positions").EnumerateArray()
                .Sum(pos => pos.GetProperty("targetAllocation").GetDecimal()));

        Assert.All(somas, soma => Assert.Equal(1.00m, soma, 4));
    }

    [Fact]
    public void PriceHistory_CobreApenasCincoDosQuinzeAtivos()
    {
        // 6 dos 11 ativos presentes nos portfólios não têm série histórica. É a origem da premissa P4:
        // calcular a volatilidade sobre o subconjunto coberto e declarar a cobertura, em vez de
        // devolver null para o portfólio inteiro e descartar a informação disponível.
        var comHistorico = Seed.RootElement.GetProperty("priceHistory")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet();

        var emCarteira = Seed.RootElement.GetProperty("portfolios")
            .EnumerateArray()
            .SelectMany(p => p.GetProperty("positions").EnumerateArray())
            .Select(p => p.GetProperty("assetSymbol").GetString()!)
            .ToHashSet();

        Assert.Equal(5, comHistorico.Count);
        Assert.Equal(15, Seed.RootElement.GetProperty("assets").GetArrayLength());

        var semHistorico = emCarteira.Except(comHistorico).ToList();
        Assert.Equal(6, semHistorico.Count);
        Assert.Contains("WEGE3", semHistorico);
    }

    [Fact]
    public void PriceHistory_ContemFinsDeSemanaEOmiteUmDiaUtil()
    {
        // A série vai de 06/09 a 06/10 incluindo sábados e domingos, mas pula 05/10. Não é um
        // calendário de pregões nem uma sequência contínua — por isso os retornos diários são
        // calculados par a par, sem interpolar ausências nem assumir espaçamento uniforme.
        var datas = Seed.RootElement.GetProperty("priceHistory").GetProperty("PETR4")
            .EnumerateArray()
            .Select(p => DateOnly.Parse(p.GetProperty("date").GetString()!))
            .OrderBy(d => d)
            .ToList();

        Assert.Contains(datas, d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.DoesNotContain(new DateOnly(2024, 10, 05), datas);
        Assert.Equal(new DateOnly(2024, 10, 06), datas.Last()); // origem da data de referência (P2)
    }

    // ── Cenário 1: Portfolio Desbalanceado ───────────────────────

    [Fact]
    public void Cenario1_TotalValueEsperadoNaoCorrespondeAoValorDeMercadoDasPosicoes()
    {
        // 800×35,50 + 200×65,20 + 400×32,10 + 300×8,75 = 56.905, e não os 51.050 declarados.
        // Diferença de 5.855 (11,5%). Nenhuma outra base reproduz 51.050: o custo das posições
        // é 53.600 e o totalInvestment é 50.000.
        var cenario = Scenario("Portfolio Desbalanceado");
        var declarado = cenario.GetProperty("expectedResults").GetProperty("totalValue").GetDecimal();

        Assert.Equal(51_050m, declarado);
        Assert.Equal(56_905m, CurrentValueOf(cenario));
        Assert.Equal(53_600m, CostBasisOf(cenario));
        Assert.Equal(50_000m, cenario.GetProperty("portfolio").GetProperty("totalInvestment").GetDecimal());
    }

    [Fact]
    public void Cenario1_AlocacoesEsperadasSomamMaisDeCemPorCento()
    {
        // 0.556 + 0.255 + 0.252 + 0.051 = 1.114. Pesos de um portfólio somam 100% por definição:
        // o conjunto é matematicamente impossível, independentemente de qual base se considere correta.
        var esperadas = Scenario("Portfolio Desbalanceado")
            .GetProperty("expectedResults").GetProperty("allocations")
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetDecimal());

        var soma = esperadas.Values.Sum();

        Assert.Equal(1.114m, soma, 3);
        Assert.True(soma > 1m, $"As alocações esperadas somam {soma:P1}.");
    }

    [Fact]
    public void Cenario1_AlocacoesEsperadasDerivamDoTotalValueIncorreto()
    {
        // Diagnóstico da origem do erro: cada peso declarado é exatamente valorPosição / 51.050.
        // O erro está no denominador (o totalValue), e se propagou para os quatro pesos —
        // razão pela qual eles somam mais de 100%.
        var cenario = Scenario("Portfolio Desbalanceado");
        var prices = CurrentPrices();
        var declaradas = cenario.GetProperty("expectedResults").GetProperty("allocations");
        var totalIncorreto = cenario.GetProperty("expectedResults").GetProperty("totalValue").GetDecimal();

        foreach (var position in cenario.GetProperty("portfolio").GetProperty("positions").EnumerateArray())
        {
            var symbol = position.GetProperty("assetSymbol").GetString()!;
            var valor = position.GetProperty("quantity").GetDecimal() * prices[symbol];

            var pesoSobreTotalIncorreto = Math.Round(valor / totalIncorreto, 3);
            var pesoDeclarado = declaradas.GetProperty(symbol).GetDecimal();

            Assert.Equal(pesoDeclarado, pesoSobreTotalIncorreto);
        }
    }

    [Fact]
    public void Cenario1_ValoresDeTradeEsperadosNaoCorrespondemAoAlvoDeVinteECincoPorCento()
    {
        // O seed sugere SELL PETR4 15.000, BUY MGLU3 9.500, BUY ITUB4 5.500.
        // Sobre o valor real (56.905), o alvo de 25% é 14.226,25 por posição, o que exige:
        // PETR4 −14.173,75 | VALE3 +1.186,25 | ITUB4 +1.386,25 | MGLU3 +11.601,25.
        // Além dos valores divergirem, VALE3 é omitido — e seu desvio (−2,08 p.p.) supera o
        // limite de 2 p.p. do enunciado, ou seja, deveria gerar trade.
        var cenario = Scenario("Portfolio Desbalanceado");
        var total = CurrentValueOf(cenario);
        var prices = CurrentPrices();
        var alvo = total * 0.25m;

        var esperados = cenario.GetProperty("expectedResults").GetProperty("suggestedActions")
            .EnumerateArray()
            .ToDictionary(a => a.GetProperty("asset").GetString()!, a => a.GetProperty("value").GetDecimal());

        Assert.Equal(14_226.25m, alvo);
        Assert.DoesNotContain("VALE3", esperados.Keys);

        var petr4Real = Math.Abs(alvo - 800 * prices["PETR4"]);
        Assert.Equal(14_173.75m, petr4Real);
        Assert.Equal(15_000m, esperados["PETR4"]);
        Assert.NotEqual(esperados["PETR4"], petr4Real);

        // O desvio de VALE3 justifica um trade que o seed não prevê.
        var vale3Deviation = (200 * prices["VALE3"] / total) - 0.25m;
        Assert.True(Math.Abs(vale3Deviation) > 0.02m);
    }

    // ── Cenário 2: Alto Risco Concentração ───────────────────────

    [Fact]
    public void Cenario2_ConcentracaoEsperadaNaoEReproduzivelPorNenhumaBaseDeCalculo()
    {
        // Declarado: 0.817. Nenhuma base plausível chega a esse número:
        //   valor de mercado   → 65.200 / 75.510 = 0.8635
        //   capital investido  → 60.000 / 70.200 = 0.8547
        //   totalInvestment    → 65.200 / 80.000 = 0.8150
        // A implementação usa o valor de mercado (0.8635): concentração é exposição presente,
        // e o preço de custo não altera o quanto se perde hoje se o ativo cair.
        var cenario = Scenario("Alto Risco Concentração");
        var prices = CurrentPrices();
        var declarado = cenario.GetProperty("expectedResults").GetProperty("concentrationRisk").GetDecimal();

        var valorDeMercado = CurrentValueOf(cenario);
        var sobreMercado = 65_200m / valorDeMercado;
        var sobreCusto = 60_000m / CostBasisOf(cenario);
        var sobreDeclarado = 65_200m / cenario.GetProperty("portfolio").GetProperty("totalInvestment").GetDecimal();

        Assert.Equal(0.817m, declarado);
        Assert.Equal(75_510m, valorDeMercado);
        Assert.Equal(0.8635m, sobreMercado, 4);
        Assert.Equal(0.8547m, sobreCusto, 4);
        Assert.Equal(0.8150m, sobreDeclarado, 4);

        Assert.All(new[] { sobreMercado, sobreCusto, sobreDeclarado },
            calculado => Assert.NotEqual(declarado, Math.Round(calculado, 3)));
    }

    [Fact]
    public void Cenario2_AlertasQualitativosEstaoCorretosApesarDoNumeroErrado()
    {
        // Vale registrar o que o seed acerta: a concentração real (86,35%) de fato supera 80%,
        // e o setor Mining tem apenas VALE3, então também supera 80%. O nível HIGH está correto.
        // O erro está confinado ao valor numérico.
        var cenario = Scenario("Alto Risco Concentração");
        var concentracaoReal = 65_200m / CurrentValueOf(cenario);

        Assert.True(concentracaoReal > 0.80m);
        Assert.Equal("HIGH", cenario.GetProperty("expectedResults").GetProperty("riskLevel").GetString());
    }

    // ── Cenário 3: Performance Calculation Test ──────────────────

    [Fact]
    public void Cenario3_EsperaVolatilidadeParaAtivosQueNaoPossuemHistorico()
    {
        // A inconsistência mais grave: o cenário compõe WEGE3 + TOTS3 e espera volatility 0.089 e
        // sharpeRatio 1.234. Nenhum dos dois ativos tem série de preços no seed — não existe cálculo
        // que produza esses números. A implementação retorna null para ambos, conforme o próprio FAQ
        // do enunciado ("Retorne null para volatilidade e documente a decisão").
        var cenario = Scenario("Performance Calculation Test");
        var comHistorico = Seed.RootElement.GetProperty("priceHistory")
            .EnumerateObject().Select(p => p.Name).ToHashSet();

        var simbolos = cenario.GetProperty("portfolio").GetProperty("positions")
            .EnumerateArray().Select(p => p.GetProperty("assetSymbol").GetString()!).ToList();

        Assert.Equal(["WEGE3", "TOTS3"], simbolos);
        Assert.All(simbolos, s => Assert.DoesNotContain(s, comHistorico));

        var expected = cenario.GetProperty("expectedResults");
        Assert.Equal(0.089m, expected.GetProperty("volatility").GetDecimal());
        Assert.Equal(1.234m, expected.GetProperty("sharpeRatio").GetDecimal());
    }

    [Fact]
    public void Cenario3_RetornoEsperadoNaoEReproduzivelPorNenhumaBaseDeCalculo()
    {
        // Declarado: 0.169. Valores reais:
        //   sobre o capital alocado → (30.245 − 27.500) / 27.500 = 0.0998
        //   sobre o totalInvestment → (30.245 − 30.000) / 30.000 = 0.0082
        // O retorno anualizado declarado (0.187) tampouco decorre de nenhum dos dois.
        var cenario = Scenario("Performance Calculation Test");
        var atual = CurrentValueOf(cenario);
        var custo = CostBasisOf(cenario);
        var declarado = cenario.GetProperty("portfolio").GetProperty("totalInvestment").GetDecimal();

        Assert.Equal(30_245m, atual);
        Assert.Equal(27_500m, custo);

        var sobreCusto = (atual - custo) / custo;
        var sobreDeclarado = (atual - declarado) / declarado;

        Assert.Equal(0.0998m, sobreCusto, 4);
        Assert.Equal(0.0082m, sobreDeclarado, 4);

        var esperado = cenario.GetProperty("expectedResults").GetProperty("totalReturn").GetDecimal();
        Assert.Equal(0.169m, esperado);
        Assert.All(new[] { sobreCusto, sobreDeclarado },
            calculado => Assert.NotEqual(esperado, Math.Round(calculado, 3)));
    }

    [Fact]
    public void TestScenarios_ExpectedResultsTemAparenciaDeValoresIlustrativos()
    {
        // Sinal que corrobora o diagnóstico: 1.234, 0.187, 0.089 e 0.817 seguem o mesmo padrão de
        // dígitos sequenciais dos exemplos do enunciado (12.34, 1.25, 15.67) e do próprio marketData
        // (0.0845, 0.1234, 0.2134, 0.3456, 0.4567). Tudo indica que os expectedResults são
        // preenchimentos ilustrativos, não resultados calculados — e por isso não servem de oráculo.
        var sharpe = Scenario("Performance Calculation Test")
            .GetProperty("expectedResults").GetProperty("sharpeRatio").GetDecimal();
        var energyReturn = Seed.RootElement.GetProperty("marketData").GetProperty("sectors")
            .EnumerateArray().Single(s => s.GetProperty("name").GetString() == "Energy")
            .GetProperty("averageReturn").GetDecimal();

        Assert.Equal(1.234m, sharpe);
        Assert.Equal(0.1234m, energyReturn);
    }
}
