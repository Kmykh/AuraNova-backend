using AuraNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
    {
        public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
        {
            builder.HasKey(x => x.Id);
            
            builder.HasIndex(x => x.AdminUserId);
            builder.HasIndex(x => x.Action);
            builder.HasIndex(x => x.EntityType);
            builder.HasIndex(x => x.CreatedAt);

            builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
            builder.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
            builder.Property(x => x.EntityId).HasMaxLength(100);
            builder.Property(x => x.IpAddress).HasMaxLength(45); // Support IPv6
            builder.Property(x => x.UserAgent).HasMaxLength(500);

            // Relación con AdminUser
            builder.HasOne(x => x.AdminUser)
                   .WithMany()
                   .HasForeignKey(x => x.AdminUserId)
                   .OnDelete(DeleteBehavior.Cascade); // O DeleteBehavior.Restrict si no queremos borrar logs al borrar usuario
        }
    }
}
