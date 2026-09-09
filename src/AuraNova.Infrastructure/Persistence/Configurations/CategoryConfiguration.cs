using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AuraNova.Domain.Entities;
using System;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
            builder.Property(c => c.Slug).IsRequired().HasMaxLength(150);
            builder.Property(c => c.Description).HasMaxLength(1000);
            builder.Property(c => c.IsActive).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();

            builder.HasIndex(c => c.Slug).IsUnique();

            // Seed initial data
            builder.HasData(
                new Category { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Flores Eternas", Slug = "flores-eternas", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
                new Category { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Accesorios", Slug = "accesorios", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
                new Category { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Maceteros", Slug = "maceteros", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
            );
        }
    }
}
