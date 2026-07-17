using PortfolioFinanceiro.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortfolioFinanceiro.Data
{
    public static class SeedDataLoader
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public static void Seed(DataContext db, string jsonPath, ILogger logger)
        {
            if (db.Portfolios.Any())
                return;

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"SeedData.json não encontrado em '{jsonPath}'.", jsonPath);

            var raw = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(jsonPath), Options)
                      ?? throw new InvalidDataException("SeedData.json não pôde ser desserializado.");

            var assets = LoadAssets(raw, logger);
            var portfolios = LoadPortfolios(raw);
            var market = BuildMarketSnapshot(raw, assets);

            db.Assets.AddRange(assets);
            db.Portfolios.AddRange(portfolios);
            db.MarketSnapshots.Add(market);
            db.SaveChanges();

            logger.LogInformation(
                "Seed carregado: {AssetCount} ativos ({WithHistory} com histórico), {PortfolioCount} portfólios (ids {Ids}), data de referência {AsOfDate}, Selic {Selic:P2}",
                assets.Count,
                assets.Count(a => a.PriceHistory.Count > 0),
                portfolios.Count,
                string.Join(", ", portfolios.Select(p => p.Id)),
                market.AsOfDate,
                market.SelicRate);
        }

        private static List<Asset> LoadAssets(SeedFile raw, ILogger logger)
        {
            var assets = raw.Assets.Select(a => new Asset
            {
                Symbol = a.Symbol,
                Name = a.Name,
                Type = a.Type,
                Sector = a.Sector,
                CurrentPrice = a.CurrentPrice,
                LastUpdated = a.LastUpdated
            }).ToList();

            var bySymbol = assets.ToDictionary(a => a.Symbol);

            foreach (var (symbol, points) in raw.PriceHistory)
            {
                if (!bySymbol.TryGetValue(symbol, out var asset))
                {
                    logger.LogWarning("Histórico de preços para símbolo desconhecido '{Symbol}' foi ignorado.", symbol);
                    continue;
                }

                // A série é ordenada por data na carga: os cálculos de retorno diário dependem da ordem,
                // e o arquivo não garante ordenação nem continuidade de calendário.
                asset.PriceHistory = points
                    .Select(p => new PricePoint { AssetSymbol = symbol, Date = p.Date, Price = p.Price })
                    .OrderBy(p => p.Date)
                    .ToList();
            }

            return assets;
        }

        private static List<Portfolio> LoadPortfolios(SeedFile raw) =>
            raw.Portfolios.Select((p, index) => new Portfolio
            {
                Id = index + 1, // premissa P1
                Name = p.Name,
                UserId = p.UserId,
                TotalInvestment = p.TotalInvestment,
                CreatedAt = p.CreatedAt,
                Positions = p.Positions.Select(pos => new Position
                {
                    AssetSymbol = pos.AssetSymbol,
                    Quantity = pos.Quantity,
                    AveragePrice = pos.AveragePrice,
                    TargetAllocation = pos.TargetAllocation,
                    LastTransaction = pos.LastTransaction
                }).ToList()
            }).ToList();

        private static MarketSnapshot BuildMarketSnapshot(SeedFile raw, List<Asset> assets)
        {
            var latestHistory = assets
                .SelectMany(a => a.PriceHistory)
                .Select(p => (DateOnly?)p.Date)
                .Max();

            var latestQuote = assets
                .Select(a => (DateOnly?)DateOnly.FromDateTime(a.LastUpdated))
                .Max();

            var asOf = new[] { latestHistory, latestQuote }
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .DefaultIfEmpty(DateOnly.FromDateTime(DateTime.UtcNow))
                .Max();

            return new MarketSnapshot
            {
                Id = 1,
                SelicRate = raw.MarketData?.SelicRate ?? 0m,
                AsOfDate = asOf
            };
        }

        // ── Contrato de leitura do arquivo (isolado do domínio) ──────────────

        private sealed record SeedFile
        {
            public List<AssetDto> Assets { get; init; } = [];
            public List<PortfolioDto> Portfolios { get; init; } = [];
            public Dictionary<string, List<PricePointDto>> PriceHistory { get; init; } = [];
            public MarketDataDto? MarketData { get; init; }
        }

        private sealed record AssetDto
        {
            public string Symbol { get; init; } = "";
            public string Name { get; init; } = "";
            public string Type { get; init; } = "";
            public string Sector { get; init; } = "";
            public decimal CurrentPrice { get; init; }
            public DateTime LastUpdated { get; init; }
        }

        private sealed record PortfolioDto
        {
            public string Name { get; init; } = "";
            public string UserId { get; init; } = "";
            public decimal TotalInvestment { get; init; }
            public DateTime CreatedAt { get; init; }
            public List<PositionDto> Positions { get; init; } = [];
        }

        private sealed record PositionDto
        {
            public string AssetSymbol { get; init; } = "";
            public decimal Quantity { get; init; }
            public decimal AveragePrice { get; init; }
            public decimal TargetAllocation { get; init; }
            public DateTime? LastTransaction { get; init; }
        }

        private sealed record PricePointDto
        {
            public DateOnly Date { get; init; }
            public decimal Price { get; init; }
        }

        private sealed record MarketDataDto
        {
            public decimal SelicRate { get; init; }
        }
    }
}
