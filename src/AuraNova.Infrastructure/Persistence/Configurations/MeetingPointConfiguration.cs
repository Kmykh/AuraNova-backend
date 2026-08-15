using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class MeetingPointConfiguration : IEntityTypeConfiguration<MeetingPoint>
    {
        public void Configure(EntityTypeBuilder<MeetingPoint> builder)
        {
            builder.ToTable("MeetingPoints");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
            builder.Property(m => m.Address).IsRequired().HasMaxLength(1000);

            builder.Property(m => m.Cost).HasPrecision(18,2).IsRequired();
            builder.ToTable(t => t.HasCheckConstraint("CK_MeetingPoint_Cost_NonNegative", "\"Cost\" >= 0"));

            builder.Property(m => m.IsActive).IsRequired();
            builder.Property(m => m.CreatedAt).IsRequired();
            builder.Property(m => m.UpdatedAt);
        }
    }
}