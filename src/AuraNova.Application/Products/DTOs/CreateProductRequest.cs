using System.Collections.Generic;

using System;
using AuraNova.Domain.Enums;

namespace AuraNova.Application.Products.DTOs
{
    public class CreateProductRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public int Stock { get; set; }

        public Guid? CategoryId { get; set; }
        public ProductAudience? Audience { get; set; }

        public List<string> AvailableColors { get; set; } = new List<string>();
        public List<string> AvailableFlowerTypes { get; set; } = new List<string>();
        public bool AllowsLights { get; set; } = false;
        public bool AllowsButterfly { get; set; } = false;
        public bool AllowsPhraseCard { get; set; } = false;
    }
}
