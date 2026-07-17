using Microsoft.EntityFrameworkCore;
using PortfolioFinanceiro.Models;

namespace PortfolioFinanceiro.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<Asset> Assets => Set<Asset>();
        public DbSet<PricePoint> PriceHistory => Set<PricePoint>();
        public DbSet<Portfolio> Portfolios => Set<Portfolio>();
        public DbSet<Position> Positions => Set<Position>();
        public DbSet<MarketSnapshot> MarketSnapshots => Set<MarketSnapshot>();
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Asset>(e =>
            {
                e.HasKey(a => a.Symbol);
                e.HasMany(a => a.PriceHistory)
                 .WithOne()
                 .HasForeignKey(p => p.AssetSymbol);
            });

            b.Entity<Portfolio>(e =>
            {
                e.HasKey(p => p.Id);
                e.HasMany(p => p.Positions)
                 .WithOne()
                 .HasForeignKey(p => p.PortfolioId);
            });

            b.Entity<Position>().HasKey(p => p.Id);
            b.Entity<PricePoint>().HasKey(p => p.Id);
            b.Entity<MarketSnapshot>().HasKey(m => m.Id);
        }
    }
}
