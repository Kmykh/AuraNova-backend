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

            var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<System.Collections.Generic.List<string>>(
                (c1, c2) => (c1 != null ? c1.Count : 0) == (c2 != null ? c2.Count : 0) && (c1 == null || c1.SequenceEqual(c2!)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            builder.Property(p => p.AvailableColors)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new System.Collections.Generic.List<string>()
                )
                .Metadata.SetValueComparer(stringListComparer);
            builder.Property(p => p.AvailableColors).HasDefaultValueSql("'[]'");

            builder.Property(p => p.AvailableFlowerTypes)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new System.Collections.Generic.List<string>()
                )
                .Metadata.SetValueComparer(stringListComparer);
            builder.Property(p => p.AvailableFlowerTypes).HasDefaultValueSql("'[]'");
        }
    }
}