namespace PortfolioFinanceiro.Models
{
    public class Asset
    {
        public required string Symbol { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        public required string Sector { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTime LastUpdated { get; set; }

        public List<PricePoint> PriceHistory { get; set; } = [];
    }
}
