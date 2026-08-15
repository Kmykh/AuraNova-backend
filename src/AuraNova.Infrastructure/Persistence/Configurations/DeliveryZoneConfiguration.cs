using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
    {
        public void Configure(EntityTypeBuilder<DeliveryZone> builder)
        {
            builder.ToTable("DeliveryZones");
            builder.HasKey(d => d.Id);

            builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
            builder.Property(d => d.District).IsRequired().HasMaxLength(200);

            builder.Property(d => d.Cost).HasPrecision(18,2).IsRequired();
            builder.ToTable(t => t.HasCheckConstraint("CK_DeliveryZone_Cost_NonNegative", "\"Cost\" >= 0"));

            builder.Property(d => d.IsActive).IsRequired();
            builder.Property(d => d.CreatedAt).IsRequired();
            builder.Property(d => d.UpdatedAt);
        }
    }
}