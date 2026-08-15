using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.OrderCode).IsRequired().HasMaxLength(100);
            builder.HasIndex(o => o.OrderCode).IsUnique();

            builder.Property(o => o.TrackingToken).IsRequired().HasMaxLength(100);
            builder.HasIndex(o => o.TrackingToken).IsUnique();

            builder.Property(o => o.DeliveryAddress).HasMaxLength(1000);
            builder.Property(o => o.Department).HasMaxLength(200);
            builder.Property(o => o.Province).HasMaxLength(200);
            builder.Property(o => o.District).HasMaxLength(200);

            builder.Property(o => o.Subtotal).HasPrecision(18,2).IsRequired();
            builder.Property(o => o.DeliveryCost).HasPrecision(18,2);
            builder.Property(o => o.Total).HasPrecision(18,2);

            builder.Property(o => o.Status).IsRequired();
            builder.Property(o => o.Notes).HasMaxLength(2000);

            builder.Property(o => o.CreatedAt).IsRequired();
            builder.Property(o => o.UpdatedAt);

            // relationships
            builder.HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(o => o.DeliveryZone).WithMany(d => d.Orders).HasForeignKey(o => o.DeliveryZoneId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(o => o.MeetingPoint).WithMany(m => m.Orders).HasForeignKey(o => o.MeetingPointId).OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(o => o.Quote).WithOne(q => q.Order).HasForeignKey<Quote>(q => q.OrderId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}