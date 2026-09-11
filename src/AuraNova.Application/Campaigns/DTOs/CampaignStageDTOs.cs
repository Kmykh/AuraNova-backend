using System;
using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Campaigns.DTOs
{
    public class CreateCampaignStageRequest
    {
        [Required(ErrorMessage = "El nombre de la etapa es requerido")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder los 150 caracteres")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "La fecha de inicio es requerida")]
        public DateTimeOffset StartDate { get; set; }

        [Required(ErrorMessage = "La fecha de fin es requerida")]
        public DateTimeOffset EndDate { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateCampaignStageRequest
    {
        [Required(ErrorMessage = "El nombre de la etapa es requerido")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder los 150 caracteres")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "La fecha de inicio es requerida")]
        public DateTimeOffset StartDate { get; set; }

        [Required(ErrorMessage = "La fecha de fin es requerida")]
        public DateTimeOffset EndDate { get; set; }

        public bool IsActive { get; set; }
    }

    public class CampaignStageResponse
    {
        public Guid Id { get; set; }
        public Guid CampaignId { get; set; }
        public string Name { get; set; } = null!;
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
