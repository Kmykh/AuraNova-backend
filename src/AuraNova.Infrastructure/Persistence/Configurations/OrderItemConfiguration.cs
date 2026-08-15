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
        }
    }
}