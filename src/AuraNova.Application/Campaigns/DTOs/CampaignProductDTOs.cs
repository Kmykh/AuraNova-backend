using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AuraNova.Application.Products.DTOs;

namespace AuraNova.Application.Campaigns.DTOs
{
    public class AddCampaignProductRequest
    {
        [Required(ErrorMessage = "El producto es requerido")]
        public Guid ProductId { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class SetCampaignProductPriceRequest
    {
        [Required(ErrorMessage = "El precio es requerido")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
        public decimal Price { get; set; }
    }

    public class CampaignProductResponse
    {
        public Guid Id { get; set; }
        public Guid CampaignId { get; set; }
        public Guid ProductId { get; set; }
        
        // Included for convenience
        public string ProductName { get; set; } = null!;
        public string? ProductImageUrl { get; set; }
        public decimal ProductBasePrice { get; set; }

        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public List<CampaignProductStagePriceResponse> StagePrices { get; set; } = new();
    }

    public class CampaignProductStagePriceResponse
    {
        public Guid Id { get; set; }
        public Guid CampaignProductId { get; set; }
        public Guid CampaignStageId { get; set; }
        public string CampaignStageName { get; set; } = null!;
        public decimal Price { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
