using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
    {
        public void Configure(EntityTypeBuilder<Quote> builder)
        {
            builder.ToTable("Quotes");
            builder.HasKey(q => q.Id);

            builder.Property(q => q.ShippingCost).HasPrecision(18,2);
            builder.Property(q => q.Notes).HasMaxLength(2000);

            builder.Property(q => q.Status).IsRequired();
            builder.Property(q => q.CreatedAt).IsRequired();
            builder.Property(q => q.UpdatedAt);
            builder.Property(q => q.QuotedAt);

            builder.HasOne(q => q.Order).WithOne(o => o.Quote).HasForeignKey<Quote>(q => q.OrderId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}