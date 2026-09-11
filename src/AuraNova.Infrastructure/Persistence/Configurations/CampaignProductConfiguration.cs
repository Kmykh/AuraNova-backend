using AuraNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class CampaignProductConfiguration : IEntityTypeConfiguration<CampaignProduct>
    {
        public void Configure(EntityTypeBuilder<CampaignProduct> builder)
        {
            builder.ToTable("CampaignProducts");
            builder.HasKey(cp => cp.Id);

            // A product can only be added once per campaign
            builder.HasIndex(cp => new { cp.CampaignId, cp.ProductId }).IsUnique();

            builder.HasOne(cp => cp.Product)
                .WithMany() // Assuming Product doesn't need a collection of CampaignProducts to keep it clean
                .HasForeignKey(cp => cp.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(cp => cp.StagePrices)
                .WithOne(sp => sp.CampaignProduct)
                .HasForeignKey(sp => sp.CampaignProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
