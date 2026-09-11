using AuraNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class CampaignProductStagePriceConfiguration : IEntityTypeConfiguration<CampaignProductStagePrice>
    {
        public void Configure(EntityTypeBuilder<CampaignProductStagePrice> builder)
        {
            builder.ToTable("CampaignProductStagePrices");
            builder.HasKey(sp => sp.Id);

            // A product can only have one price per stage in a campaign
            builder.HasIndex(sp => new { sp.CampaignProductId, sp.CampaignStageId }).IsUnique();

            builder.Property(sp => sp.Price).HasColumnType("decimal(18,2)");
            
            // Check constraint to ensure price > 0
            builder.ToTable(t => t.HasCheckConstraint("CK_CampaignProductStagePrice_Price", "Price > 0"));
        }
    }
}
