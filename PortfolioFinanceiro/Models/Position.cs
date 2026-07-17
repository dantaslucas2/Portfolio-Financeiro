namespace PortfolioFinanceiro.Models
{
    public class Position
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public string AssetSymbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal TargetAllocation { get; set; }
        public DateTime? LastTransaction { get; set; }

    }
}
