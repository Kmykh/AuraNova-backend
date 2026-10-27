using System;
using System.Collections.Generic;

using AuraNova.Application.Categories.DTOs;
using AuraNova.Domain.Enums;

namespace AuraNova.Application.Products.DTOs
{
    public class ProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        /// <summary>Portada (la primera de <see cref="Images"/>).</summary>
        public string? ImageUrl { get; set; }
        /// <summary>Galería en orden; incluye la portada.</summary>
        public List<string> Images { get; set; } = new List<string>();
        public int Stock { get; set; }
        public bool IsAvailable { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public CategoryResponse? Category { get; set; }
        public ProductAudience? Audience { get; set; }

        public List<string> AvailableColors { get; set; } = new List<string>();
        public List<string> AvailableFlowerTypes { get; set; } = new List<string>();
        public bool AllowsLights { get; set; } = false;
        public bool AllowsButterfly { get; set; } = false;
        public bool AllowsPhraseCard { get; set; } = false;
        /// <summary>Claves de los accesorios que ofrece (los precios están en las opciones de personalización).</summary>
        public List<string> AvailableAccessories { get; set; } = new List<string>();
        /// <summary>Cuántos colores y tipos de flor puede elegir el cliente (1 = flor por unidad).</summary>
        public int? MaxChoices { get; set; }
        
        // Campaign Fields
        public decimal EffectivePrice { get; set; }
        public bool IsCampaignActive { get; set; }
        public Guid? CampaignId { get; set; }
        public string? CampaignName { get; set; }
        public string? CampaignStageName { get; set; }
        /// <summary>Hasta cuándo dura el precio de la etapa actual.</summary>
        public DateTimeOffset? CampaignStageEndsAt { get; set; }
        /// <summary>Solo en el detalle: todas las etapas de la campaña con el precio de este producto.</summary>
        public List<ProductCampaignStageResponse>? CampaignStages { get; set; }
    }

    public class ProductCampaignStageResponse
    {
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public decimal Price { get; set; }
        public bool IsCurrent { get; set; }
    }
}
