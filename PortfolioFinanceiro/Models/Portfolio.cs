namespace PortfolioFinanceiro.Models
{
    public class Portfolio
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string UserId { get; set; }
        public decimal TotalInvestment { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Position> Positions { get; set; } = [];

    }
}
