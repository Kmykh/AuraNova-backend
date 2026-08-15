using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.MeetingPoints.DTOs
{
    public class CreateMeetingPointRequest
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "La dirección es obligatoria.")]
        [MaxLength(500)]
        public string Address { get; set; } = null!;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "El costo debe ser mayor o igual a 0.")]
        public decimal Cost { get; set; }
    }
}
