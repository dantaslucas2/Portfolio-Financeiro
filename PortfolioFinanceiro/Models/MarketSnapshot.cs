namespace PortfolioFinanceiro.Models
{
    public class MarketSnapshot
    {
        public int Id { get; set; }
        public decimal SelicRate { get; set; }
        public DateOnly AsOfDate { get; set; }

    }
}
