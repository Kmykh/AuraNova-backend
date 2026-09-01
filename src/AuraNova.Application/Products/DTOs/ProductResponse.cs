using System;
using System.Collections.Generic;

namespace AuraNova.Application.Products.DTOs
{
    public class ProductResponse
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

        public List<string> AvailableColors { get; set; } = new List<string>();
        public List<string> AvailableFlowerTypes { get; set; } = new List<string>();
        public bool AllowsLights { get; set; } = false;
        public bool AllowsButterfly { get; set; } = false;
        public bool AllowsPhraseCard { get; set; } = false;
    }
}
