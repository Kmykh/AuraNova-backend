using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");
            builder.HasKey(oi => oi.Id);

            builder.Property(oi => oi.Quantity).IsRequired();
            builder.ToTable(t => t.HasCheckConstraint("CK_OrderItem_Quantity_Positive", "\"Quantity\" > 0"));

            builder.Property(oi => oi.UnitPrice).HasPrecision(18,2).IsRequired();
            builder.ToTable(t => t.HasCheckConstraint("CK_OrderItem_UnitPrice_NonNegative", "\"UnitPrice\" >= 0"));

            builder.Property(oi => oi.Subtotal).HasPrecision(18,2).IsRequired();
            builder.ToTable(t => t.HasCheckConstraint("CK_OrderItem_Subtotal_NonNegative", "\"Subtotal\" >= 0"));

            builder.HasOne(oi => oi.Product).WithMany(p => p.OrderItems).HasForeignKey(oi => oi.ProductId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(oi => oi.Order).WithMany(o => o.Items).HasForeignKey(oi => oi.OrderId).OnDelete(DeleteBehavior.Cascade);

            builder.Property(oi => oi.SelectedPrimaryColor).HasMaxLength(50);
            builder.Property(oi => oi.SelectedSecondaryColor).HasMaxLength(50);
            builder.Property(oi => oi.SelectedFlowerType).HasMaxLength(100);
            builder.Property(oi => oi.SelectedFlowerColor).HasMaxLength(50);
            builder.Property(oi => oi.PhraseText).HasMaxLength(1000);
            builder.Property(oi => oi.PhraseFont).HasMaxLength(50);
            builder.Property(oi => oi.PhraseTo).HasMaxLength(60);
            builder.Property(oi => oi.PhraseFrom).HasMaxLength(60);
            builder.Property(oi => oi.PhraseTheme).HasMaxLength(20);
            builder.Property(oi => oi.CardBackgroundPath).HasMaxLength(300);

            // Opciones elegidas como lista JSON: una por elemento
            var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                (c1, c2) => (c1 != null ? c1.Count : 0) == (c2 != null ? c2.Count : 0) && (c1 == null || c1.SequenceEqual(c2!)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            builder.Property(oi => oi.SelectedColors)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);
            builder.Property(oi => oi.SelectedColors).HasDefaultValueSql("'[]'");

            builder.Property(oi => oi.SelectedFlowerTypes)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);
            builder.Property(oi => oi.SelectedFlowerTypes).HasDefaultValueSql("'[]'");
        }
    }
}