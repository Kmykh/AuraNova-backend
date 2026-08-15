using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");
            builder.HasKey(n => n.Id);

            builder.Property(n => n.Type).IsRequired();
            builder.Property(n => n.Channel).IsRequired();
            builder.Property(n => n.Status).IsRequired();

            builder.Property(n => n.Recipient).IsRequired().HasMaxLength(50);
            builder.Property(n => n.Subject).HasMaxLength(200);
            builder.Property(n => n.Message).IsRequired().HasColumnType("text");
            builder.Property(n => n.ChannelUrl).HasMaxLength(2000);
            builder.Property(n => n.ErrorMessage).HasColumnType("text");

            builder.Property(n => n.CreatedAt).IsRequired();

            builder.HasOne(n => n.Order)
                .WithMany(o => o.Notifications)
                .HasForeignKey(n => n.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes as requested
            builder.HasIndex(n => n.OrderId);
            builder.HasIndex(n => n.Status);
            builder.HasIndex(n => n.CreatedAt);
        }
    }
}
