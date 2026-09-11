using AuraNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class CampaignStageConfiguration : IEntityTypeConfiguration<CampaignStage>
    {
        public void Configure(EntityTypeBuilder<CampaignStage> builder)
        {
            builder.ToTable("CampaignStages");
            builder.HasKey(s => s.Id);
            
            builder.Property(s => s.Name).IsRequired().HasMaxLength(150);

            builder.HasMany(s => s.StagePrices)
                .WithOne(sp => sp.CampaignStage)
                .HasForeignKey(sp => sp.CampaignStageId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
