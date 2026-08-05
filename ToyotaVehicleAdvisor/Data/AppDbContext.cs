using ToyotaVehicleAdvisor.Models;
using Microsoft.EntityFrameworkCore;

namespace ToyotaVehicleAdvisor.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<ToyotaCar> ToyotaCars => Set<ToyotaCar>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ToyotaCar>(entity =>
            {
                entity.HasKey(car => car.Id);
                entity.Property(car => car.Model).HasMaxLength(80).IsRequired();
                entity.Property(car => car.Category).HasMaxLength(40).IsRequired();
                entity.Property(car => car.FuelType).HasMaxLength(40).IsRequired();
                entity.Property(car => car.BestFor).HasMaxLength(240).IsRequired();
                entity.Property(car => car.Description).HasMaxLength(500).IsRequired();
                entity.Property(car => car.SourceUrl).HasMaxLength(300).IsRequired();
                entity.Property(car => car.StartingPriceWan).HasPrecision(6, 1);
                entity.Property(car => car.MaxPriceWan).HasPrecision(6, 1);
                entity.Property(car => car.FuelEconomyKmPerLiter).HasPrecision(5, 1);
            });
        }
    }
}
