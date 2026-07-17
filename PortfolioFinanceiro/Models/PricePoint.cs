namespace PortfolioFinanceiro.Models
{
    public class PricePoint
    {
        public int Id { get; set; }
        public required string AssetSymbol { get; set; }
        public DateOnly Date { get; set; }
        public decimal Price { get; set; }
    }
}
