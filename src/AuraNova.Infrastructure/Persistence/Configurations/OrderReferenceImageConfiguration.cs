using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class OrderReferenceImageConfiguration : IEntityTypeConfiguration<OrderReferenceImage>
    {
        public void Configure(EntityTypeBuilder<OrderReferenceImage> builder)
        {
            builder.ToTable("OrderReferenceImages");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Path).IsRequired().HasMaxLength(500);
            builder.Property(i => i.CreatedAt).IsRequired();
            builder.HasIndex(i => new { i.OrderId, i.SortOrder });
        }
    }
}
