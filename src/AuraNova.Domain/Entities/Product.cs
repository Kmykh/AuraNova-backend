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

        // Customization Configuration
        public List<string> AvailableColors { get; set; } = new List<string>();
        public List<string> AvailableFlowerTypes { get; set; } = new List<string>();
        public bool AllowsLights { get; set; } = false;
        public bool AllowsButterfly { get; set; } = false;
        public bool AllowsPhraseCard { get; set; } = false;

        public ICollection<OrderItem>? OrderItems { get; set; }

        public Product()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            IsAvailable = true;
        }
    }
}