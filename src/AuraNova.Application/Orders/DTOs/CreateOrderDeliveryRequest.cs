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

        /// <summary>Quién recibe, si no es el mismo cliente (regalos).</summary>
        [MaxLength(120)]
        public string? RecipientName { get; set; }

        [MaxLength(20)]
        public string? RecipientPhone { get; set; }

        /// <summary>DNI (8 dígitos) o CE de quien recoge en la agencia.</summary>
        [MaxLength(15)]
        public string? RecipientDocument { get; set; }

        [MaxLength(250)]
        public string? DeliveryReference { get; set; }

        /// <summary>Shalom u Olva (envío nacional).</summary>
        [MaxLength(30)]
        public string? PreferredAgency { get; set; }
    }
}
