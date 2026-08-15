using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
    {
        public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
        {
            builder.ToTable("OrderStatusHistory");
            builder.HasKey(h => h.Id);

            builder.Property(h => h.Status).IsRequired();
            builder.Property(h => h.Comment).HasMaxLength(2000);
            builder.Property(h => h.CreatedAt).IsRequired();

            builder.HasOne(h => h.Order)
                .WithMany(o => o.StatusHistory)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite index for efficient history retrieval
            builder.HasIndex(h => new { h.OrderId, h.CreatedAt });
        }
    }
}
