using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateOrderDeliveryRequest
    {
        [Required(ErrorMessage = "El tipo de entrega es obligatorio.")]
        public string Type { get; set; } = null!;

        public Guid? DeliveryZoneId { get; set; }
        public Guid? MeetingPointId { get; set; }

        [MaxLength(1000)]
        public string? DeliveryAddress { get; set; }

        [MaxLength(200)]
        public string? Department { get; set; }

        [MaxLength(200)]
        public string? Province { get; set; }

        [MaxLength(200)]
        public string? District { get; set; }
    }
}
