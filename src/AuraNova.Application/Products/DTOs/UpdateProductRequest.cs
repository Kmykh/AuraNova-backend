using System.Collections.Generic;

using System;
using AuraNova.Domain.Enums;

namespace AuraNova.Application.Products.DTOs
{
    public class UpdateProductRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        /// <summary>
        /// Galería completa en orden (la primera es la portada). Si se envía, reemplaza a <see cref="ImageUrl"/>;
        /// si es null se mantiene el comportamiento anterior de una sola imagen.
        /// </summary>
        public List<string>? ImageUrls { get; set; }

        public Guid? CategoryId { get; set; }
        public ProductAudience? Audience { get; set; }

        public List<string> AvailableColors { get; set; } = new List<string>();
        public List<string> AvailableFlowerTypes { get; set; } = new List<string>();
        public bool AllowsLights { get; set; } = false;
        public bool AllowsButterfly { get; set; } = false;
        public bool AllowsPhraseCard { get; set; } = false;

        /// <summary>Cuántos colores y tipos de flor puede elegir el cliente (1 = flor por unidad). Vacío = sin límite propio.</summary>
        [System.ComponentModel.DataAnnotations.Range(1, 5)]
        public int? MaxChoices { get; set; }
    }
}
