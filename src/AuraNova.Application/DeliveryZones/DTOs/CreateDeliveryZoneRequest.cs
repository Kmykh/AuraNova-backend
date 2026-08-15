using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.DeliveryZones.DTOs
{
    public class CreateDeliveryZoneRequest
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "El distrito es obligatorio.")]
        [MaxLength(200)]
        public string District { get; set; } = null!;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "El costo debe ser mayor o igual a 0.")]
        public decimal Cost { get; set; }
    }
}
