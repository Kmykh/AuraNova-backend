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

            builder.Property(x => x.YapeQrImageUrl)
                .HasMaxLength(500);

            var colorComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<ColorOption>>(
                (a, b) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null) ==
                          System.Text.Json.JsonSerializer.Serialize(b, (System.Text.Json.JsonSerializerOptions?)null),
                c => System.Text.Json.JsonSerializer.Serialize(c, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
                c => c.Select(o => new ColorOption { Name = o.Name, Hex = o.Hex }).ToList());

            builder.Property(x => x.ColorPalette)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<ColorOption>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<ColorOption>())
                .Metadata.SetValueComparer(colorComparer);
            builder.Property(x => x.ColorPalette).HasDefaultValueSql("'[]'");

            var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                (c1, c2) => (c1 != null ? c1.Count : 0) == (c2 != null ? c2.Count : 0) && (c1 == null || c1.SequenceEqual(c2!)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            builder.Property(x => x.FlowerTypeOptions)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);
            builder.Property(x => x.FlowerTypeOptions).HasDefaultValueSql("'[]'");
        }
    }
}
