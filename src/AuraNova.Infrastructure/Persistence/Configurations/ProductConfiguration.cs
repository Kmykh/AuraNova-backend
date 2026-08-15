using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name).IsRequired().HasMaxLength(250);
            builder.Property(p => p.Description).HasMaxLength(2000);

            builder.Property(p => p.Price).HasPrecision(18,2).IsRequired();
            builder.ToTable(t => t.HasCheckConstraint("CK_Product_Price_NonNegative", "\"Price\" >= 0"));

            builder.Property(p => p.Stock).IsRequired();
            builder.ToTable(t => t.HasCheckConstraint("CK_Product_Stock_NonNegative", "\"Stock\" >= 0"));

            builder.Property(p => p.ImageUrl).HasMaxLength(1000);
            builder.Property(p => p.IsAvailable).IsRequired();

            builder.Property(p => p.CreatedAt).IsRequired();
            builder.Property(p => p.UpdatedAt);
        }
    }
}