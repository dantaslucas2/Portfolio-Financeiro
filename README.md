# Portfolio-Financeiro — Analytics de Portfólio

WebAPI em .NET 8 com três endpoints analíticos — performance, análise de risco e sugestão
de rebalanceamento — sobre portfólios de investimento carregados de um arquivo de seed. É
um desafio técnico, então não há cadastro, autenticação nem persistência: só os cálculos
sobre os três portfólios do seed.

A ideia que guiou o projeto foi tratar dado inconsistente de forma explícita. O seed tem
inconsistências propositais, e em vez de deixar a API quebrar ou devolver um número errado
sem avisar, cada resposta carrega uma lista de `warnings` com o que foi detectado durante
aquele cálculo. A seção "Divergências encontradas no SeedData", mais abaixo, detalha o que
foi encontrado.

## Como executar

```bash
dotnet run --project Portfolio-Financeiro
```

A raiz redireciona para o Swagger UI:

- HTTP: `http://localhost:5161/swagger`
- HTTPS: `https://localhost:7257/swagger`

O seed é carregado na inicialização, de `Portfolio-Financeiro/Data/SeedData.json`. Os três
portfólios ficam disponíveis pelos IDs 1 (Conservador), 2 (Crescimento) e 3 (Dividendos).

## Como testar

```bash
dotnet test
```

São 32 testes, em quatro grupos:

- `FinancialMathTests` — as fórmulas puras (retorno, anualização, retornos diários, desvio
  padrão amostral, Sharpe, HHI, conversões). Rodam sem banco nem DI; se algo aqui quebra,
  os três endpoints quebram junto.
- `PerformanceCalculatorTests`, `RiskAnalyzerTests`, `RebalancingOptimizerTests` — os
  services, com fixtures montadas via `TestData` e valores conferidos à mão.
- `AnalyticsEndpointsTests` — integração ponta a ponta via `WebApplicationFactory<Program>`,
  batendo nos endpoints reais para os três portfólios e nos casos de erro.
- `SeedDataAuditTests` — a auditoria do seed. Não testa a aplicação: lê o `SeedData.json`
  real e prova, com números recalculados do próprio arquivo, cada inconsistência descrita
  na seção de divergências.

## Arquitetura

```
Portfolio-Financeiro/
├── Controllers/
│   └── AnalyticsController.cs   # os 3 endpoints GET; valida o ID e traduz ausência em 404
├── Services/
│   ├── FinancialMath.cs         # fórmulas puras (retorno, volatilidade, Sharpe, HHI...)
│   ├── PortfolioValuator.cs     # valoração centralizada + detecção de inconsistências
│   ├── PerformanceCalculator.cs
│   ├── RiskAnalyzer.cs
│   ├── RebalancingOptimizer.cs
│   └── Interfaces/
├── Models/
│   ├── Asset.cs, Portfolio.cs, Position.cs, PricePoint.cs, MarketSnapshot.cs
│   ├── ValuedPortfolio.cs       # modelo interno de valoração; não é exposto pela API
│   └── DTOs/                    # contratos de saída dos três endpoints
├── Data/
│   ├── DataContext.cs           # EF Core InMemory
│   ├── SeedDataLoader.cs        # carga do JSON, atribuição de IDs, data de referência
│   └── SeedData.json
└── Program.cs                   # bootstrap, DI, Swagger, seed no startup
```

É uma WebAPI pura: `ControllerBase` com `AddControllers()`, sem Views nem Razor.

### Endpoints

| Rota | Retorna | Erros |
|------|---------|-------|
| `GET /api/portfolios/{id}/performance` | `PerformanceResponse` | 400 se `id ≤ 0`; 404 se não existe |
| `GET /api/portfolios/{id}/risk-analysis` | `RiskAnalysisResponse` | 400 se `id ≤ 0`; 404 se não existe |
| `GET /api/portfolios/{id}/rebalancing` | `RebalancingResponse` | 400 se `id ≤ 0`; 404 se não existe |

Uma rota com ID não numérico (`/api/portfolios/abc/performance`) nem chega ao controller: a
restrição `{id:int}` já responde 404.

### O PortfolioValuator

O fluxo de uma requisição é `Controller → Service → PortfolioValuator.ValueAsync →
DataContext`. O valorizador busca o portfólio e o snapshot de mercado, cruza as posições
com o cadastro de ativos e devolve um `ValuedPortfolio` com valor investido e atual, peso
de cada posição, cobertura de histórico e os avisos gerados. Cada service consome esse
mesmo objeto e o mapeia para o seu DTO.

Ele não estava no escopo pedido — o enunciado lista só os três services. Criei-o porque os
três precisam exatamente do mesmo insumo, e principalmente do mesmo tratamento para os
dados sujos do seed (posição sem ativo, preço zerado, alocação-alvo que não fecha em 100%).
Calcular isso três vezes abriria espaço para os três endpoints divergirem entre si no valor
e nos pesos de um mesmo portfólio. Centralizado, o comportamento é único e fica testável em
um lugar só.

### Exemplo — GET /api/portfolios/1/performance

O Conservador é um bom exemplo do tratamento de inconsistências: o `totalInvestment`
declarado (100.000) não bate com o capital alocado nas posições (76.800), e um dos cinco
ativos (WEGE3) não tem histórico de preços.

```json
{
  "totalInvestment": 100000.00,
  "investedAmount": 76800.00,
  "currentValue": 80940.00,
  "totalReturn": 5.39,
  "totalReturnAmount": 4140.00,
  "totalReturnOnInvestment": 4.14,
  "annualizedReturn": 7.50,
  "volatility": 19.98,
  "volatilityCoverage": 0.8941,
  "asOfDate": "2024-10-06",
  "periodInDays": 265,
  "positionsPerformance": [
    { "symbol": "PETR4", "quantity": 500, "averagePrice": 30.00, "currentPrice": 35.50, "investedAmount": 15000.00, "currentValue": 17750.00, "return": 18.33, "weight": 21.93, "volatility": 32.58 },
    { "symbol": "VALE3", "quantity": 300, "averagePrice": 60.00, "currentPrice": 65.20, "investedAmount": 18000.00, "currentValue": 19560.00, "return": 8.67, "weight": 24.17, "volatility": 31.41},
    { "symbol": "BBDC4", "quantity": 1000, "averagePrice": 18.00, "currentPrice": 15.80, "investedAmount": 18000.00, "currentValue": 15800.00, "return": -12.22, "weight": 19.52, "volatility": 35.16 },
    { "symbol": "ITUB4", "quantity": 600, "averagePrice": 28.00, "currentPrice": 32.10, "investedAmount": 16800.00, "currentValue": 19260.00, "return": 14.64, "weight": 23.80, "volatility": 31.83 },
    { "symbol": "WEGE3", "quantity": 200, "averagePrice": 45.00, "currentPrice": 42.85, "investedAmount": 9000.00, "currentValue": 8570.00, "return": -4.78, "weight": 10.59, "volatility": null }
  ],
  "warnings": [
    "Sem histórico de preços para WEGE3. A volatilidade foi calculada sobre 89.41% do valor do portfólio.",
    "O campo 'totalInvestment' (100000.00) diverge do capital alocado nas posições (76800.00). O retorno principal usa o capital alocado; 'totalReturnOnInvestment' expõe a métrica sobre o valor declarado."
  ]
}
```

## Fórmulas financeiras

Internamente tudo fica em `decimal`, como fração (0.0539, não 5.39%). A conversão para
percentual só acontece ao montar o DTO.

| Cálculo | Fórmula | Nota |
|---|---|---|
| Retorno total | (valorAtual − capitalAlocado) / capitalAlocado | Sobre o capital alocado nas posições, não sobre o `totalInvestment` declarado |
| Retorno sobre valor declarado | SimpleReturn(declarado, valorAtual + max(declarado − alocado, 0)) | Métrica secundária; trata a diferença declarado − alocado como caixa parado |
| Retorno anualizado | (1 + retornoTotal)^(365 / dias) − 1 | Dias = data de referência do seed − criação do portfólio; nulo se o período não é positivo |
| Retornos diários | preço[i] / preço[i−1] − 1 | Ignora pares com preço anterior não positivo |
| Volatilidade diária | desvio padrão amostral (n − 1) | Nulo com menos de duas observações |
| Volatilidade anualizada (ativo) | volatilidadeDiária × √252 | 252 pregões por ano |
| Volatilidade do portfólio | desvio padrão dos retornos de uma série de valor reconstruída, × √252 | Somei quantidade × preço por data, usando só ativos com histórico e só nas datas comuns a todos. Não é média ponderada das volatilidades individuais — isso ignoraria a correlação entre os ativos |
| Índice de Sharpe | (retornoAnualizado − Selic) / volatilidadeAnualizada | Selic vem do seed; nulo se faltar retorno ou volatilidade |
| Concentração (HHI) | soma dos pesos² | Também exponho 1/HHI como "posições efetivas", junto do peso da maior posição e da soma das três maiores |
| Desvio de alocação | pesoAtual − pesoAlvo | Só vira trade se o desvio absoluto passa de 2 pontos percentuais |
| Trade sugerido | quantidade = trunc(\|valorAlvo − valorAtual\| / preço) | Quantidade inteira; venda limitada ao que há em carteira; descarta trade abaixo de R$ 100; custo de 0,3% sobre o valor |

## Premissas adotadas

| Premissa | Por quê |
|---|---|
| IDs 1, 2, 3 atribuídos na carga, na ordem do arquivo | O seed não traz `id`, mas a rota é `/portfolios/{id}` |
| Data de referência fixa (2024-10-06, a mais recente do seed) em vez de `DateTime.Now` | Deixa os cálculos determinísticos — os testes não passam a falhar com o tempo, e o avaliador reproduz os mesmos números |
| Retorno principal sobre o capital alocado; `totalReturnOnInvestment` como métrica separada | O `totalInvestment` declarado diverge das posições nos três portfólios (ver divergências); usei a base que representa o capital em risco, mas mantive a outra visível |
| Volatilidade sobre o subconjunto de ativos com histórico, com `volatilityCoverage` | 6 dos 11 ativos não têm série; preferi calcular sobre o que dá e declarar a cobertura a devolver `null` para o portfólio inteiro |
| `testScenarios` do seed usados só como fixtures de teste, não carregados na API | Seus `expectedResults` contêm os erros descritos abaixo |
| Alocações-alvo normalizadas proporcionalmente quando não somam 100%, com aviso | Rejeitar o portfólio por erro de cadastro impediria qualquer análise; aceitar sem normalizar geraria desvios artificiais no rebalanceamento |
| Posição sem ativo cadastrado é excluída do cálculo, com aviso | Assumir preço zero afundaria o retorno e distorceria todos os pesos |

## Divergências encontradas no SeedData

O `Data/SeedData.json` tem inconsistências deliberadas. Os valores abaixo foram
recalculados direto do arquivo, com as mesmas fórmulas de `PortfolioValuator` e
`FinancialMath` — dá para reproduzir relendo o JSON. Onde há teste cobrindo o valor real,
ele é citado. Todos os testes de `SeedDataAuditTests` passam: o que eles afirmam é a
divergência.

**Portfólios sem identificador.** Nenhum dos três traz `id`; ele é atribuído na carga.

**`totalInvestment` diverge do capital alocado**, nos três:

| Portfólio | Declarado | Alocado (Σ quantidade × preço médio) | Diferença |
|---|---|---|---|
| Conservador | 100.000,00 | 76.800,00 | +23.200,00 |
| Crescimento | 250.000,00 | 158.620,00 | +91.380,00 |
| Dividendos | 150.000,00 | 97.400,00 | +52.600,00 |

O Conservador é travado por
`AnalyticsEndpointsTests.Performance_DoPortfolioConservador_RetornaValoresConferidosAMao` e
`PerformanceCalculatorTests.CalculateAsync_CalculaRetornoTotalSobreCapitalAlocado`.

**6 dos 11 ativos em posições não têm histórico de preços.** `WEGE3`, `TOTS3`, `RENT3`,
`VIVT3`, `CCRO3` e `B3SA3` não aparecem em `priceHistory`; só `PETR4`, `VALE3`, `BBDC4`,
`ITUB4` e `MGLU3` têm. É o que força o `volatilityCoverage` a existir — no Conservador,
WEGE3 sem série deixa a cobertura em 89,41%.

**Cenário "Portfolio Desbalanceado".** O `totalValue` declarado (51.050,00) não corresponde
ao valor de mercado das posições (56.905,00). As quatro `allocations` esperadas foram
calculadas dividindo cada posição por esse total errado, e por isso somam 111,4%:

| Ativo | Alocação esperada (sobre o total errado) | Alocação real (sobre 56.905,00) |
|---|---|---|
| PETR4 | 55,6% | 49,9% |
| VALE3 | 25,5% | 22,9% |
| ITUB4 | 25,2% | 22,6% |
| MGLU3 | 5,1% | 4,6% |
| **Soma** | **111,4%** | **100,0%** |

**Cenário "Alto Risco Concentração".** O `concentrationRisk` declarado (0,817) não sai de
nenhuma base plausível — mercado 0,8635, custo 0,8547, `totalInvestment` 0,8150. O nível
`HIGH`, porém, está certo nas três: todas passam folgado do limiar de concentração alta.

**Cenário "Performance Calculation Test".** Espera volatilidade e Sharpe para um portfólio
só de `WEGE3` e `TOTS3` — os dois sem histórico. Não há como calcular; a implementação
retorna ambos como nulos.

A implementação segue os valores recalculados, não os `expectedResults` do seed.

## Tratamento de edge cases

- Divisão por zero: o retorno de uma posição só é calculado com preço médio positivo, senão
  é nulo; preço atual não positivo zera o valor de mercado da posição; valor total zero
  deixa todos os pesos em zero, não indeterminados.
- Ausência de histórico: volatilidade do ativo e do portfólio retornam nulo, nunca zero, e
  a resposta expõe `volatilityCoverage`.
- Alocações-alvo fora de 100%: normalizadas proporcionalmente, com aviso; se a soma for zero
  ou negativa, o rebalanceamento é marcado como não aplicável.
- Portfólio inexistente: 404 com `ProblemDetails` (RFC 7807) listando os IDs válidos; ID
  zero ou negativo retorna 400 antes de consultar o banco.
- Rebalanceamento: quantidade truncada para inteiro, venda limitada ao que há em carteira, e
  trade abaixo de R$ 100 é descartado.

## Diferenciais implementados

- Swagger/OpenAPI (Swashbuckle), com a raiz redirecionando para a documentação.
- Erros como `ProblemDetails` (RFC 7807).
- Logs estruturados em cada cálculo (`ILogger` com placeholders nomeados), na carga do seed
  e nos três services.
- Testes de integração via `WebApplicationFactory<Program>`, além dos unitários.
- Cada resposta carrega seus próprios `warnings`.
- Métricas além do mínimo nos DTOs: `investedAmount`, `volatilityCoverage`,
  `herfindahlIndex`, `effectiveNumberOfPositions`, `resultingWeight` por trade e um
  `expectedImprovement` com a redução do desvio agregado antes/depois dos trades.
