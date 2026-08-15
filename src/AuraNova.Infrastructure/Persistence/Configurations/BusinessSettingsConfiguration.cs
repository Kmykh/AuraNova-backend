using AuraNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class BusinessSettingsConfiguration : IEntityTypeConfiguration<AuraNova.Domain.Entities.BusinessSettings>
    {
        public void Configure(EntityTypeBuilder<AuraNova.Domain.Entities.BusinessSettings> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.BusinessName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.WhatsAppNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.YapeHolderName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.TrackingBaseUrl)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.YapeQrImageUrl)
                .HasMaxLength(500);
        }
    }
}
