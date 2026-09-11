using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Campaigns.DTOs
{
    public class CreateCampaignRequest
    {
        [Required(ErrorMessage = "El nombre de la campaña es requerido")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder los 150 caracteres")]
        public string Name { get; set; } = null!;

        [StringLength(500, ErrorMessage = "La descripción no puede exceder los 500 caracteres")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es requerida")]
        public DateTimeOffset StartDate { get; set; }

        [Required(ErrorMessage = "La fecha de fin es requerida")]
        public DateTimeOffset EndDate { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateCampaignRequest
    {
        [Required(ErrorMessage = "El nombre de la campaña es requerido")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder los 150 caracteres")]
        public string Name { get; set; } = null!;

        [StringLength(500, ErrorMessage = "La descripción no puede exceder los 500 caracteres")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es requerida")]
        public DateTimeOffset StartDate { get; set; }

        [Required(ErrorMessage = "La fecha de fin es requerida")]
        public DateTimeOffset EndDate { get; set; }

        public bool IsActive { get; set; }
    }

    public class CampaignResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        
        public List<CampaignStageResponse> Stages { get; set; } = new();
        public List<CampaignProductResponse> Products { get; set; } = new();
    }
}
