using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
    {
        public void Configure(EntityTypeBuilder<ProductImage> builder)
        {
            builder.ToTable("ProductImages");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Path).IsRequired().HasMaxLength(1000);
            builder.Property(i => i.CreatedAt).IsRequired();
            builder.HasIndex(i => new { i.ProductId, i.SortOrder });
            builder.HasIndex(i => i.Path);

            builder.HasOne(i => i.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
