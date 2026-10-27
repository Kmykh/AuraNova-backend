using System;
using System.Collections.Generic;

namespace AuraNova.Domain.Entities
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public int Stock { get; set; }
        public bool IsAvailable { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public Guid? CategoryId { get; set; }
        public Category? Category { get; set; }
        public AuraNova.Domain.Enums.ProductAudience? Audience { get; set; }

        // Customization Configuration
        public List<string> AvailableColors { get; set; } = new List<string>();
        public List<string> AvailableFlowerTypes { get; set; } = new List<string>();
        public bool AllowsLights { get; set; } = false;
        public bool AllowsButterfly { get; set; } = false;
        public bool AllowsPhraseCard { get; set; } = false;

        /// <summary>
        /// Cuántos colores y tipos de flor puede elegir el cliente. 1 = flor por unidad (un solo color);
        /// null = sin límite propio (hasta el máximo general).
        /// </summary>
        public int? MaxChoices { get; set; }

        /// <summary>Galería ordenada; la primera es la portada (igual a <see cref="ImageUrl"/>).</summary>
        public ICollection<ProductImage>? Images { get; set; }

        public ICollection<OrderItem>? OrderItems { get; set; }

        public Product()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            IsAvailable = true;
        }
    }
}