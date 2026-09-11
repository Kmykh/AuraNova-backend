using AuraNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
    {
        public void Configure(EntityTypeBuilder<Campaign> builder)
        {
            builder.ToTable("Campaigns");
            builder.HasKey(c => c.Id);
            
            builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
            builder.Property(c => c.Description).HasMaxLength(500);

            builder.HasMany(c => c.Stages)
                .WithOne(s => s.Campaign)
                .HasForeignKey(s => s.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(c => c.Products)
                .WithOne(p => p.Campaign)
                .HasForeignKey(p => p.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
