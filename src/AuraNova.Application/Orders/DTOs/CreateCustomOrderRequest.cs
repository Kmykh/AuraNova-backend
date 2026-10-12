using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateCustomOrderRequest
    {
        [Required]
        public CreateOrderCustomerRequest Customer { get; set; } = null!;

        [Required]
        public CreateOrderDeliveryRequest Delivery { get; set; } = null!;

        /// <summary>Compatibilidad: una sola imagen en base64. El formulario nuevo envía archivos (multipart).</summary>
        public string? ReferenceImageUrl { get; set; }
        
        [MaxLength(2000)]
        public string? CustomizationNotes { get; set; }

        /// <summary>Enlaces de inspiración (Pinterest, TikTok, Instagram…). Máximo 5.</summary>
        [MaxLength(5)]
        public List<string>? ReferenceLinks { get; set; }

        /// <summary>Fecha en la que el cliente necesita el pedido.</summary>
        public DateOnly? DesiredDate { get; set; }

        [MaxLength(60)]
        public string? Occasion { get; set; }

        [MaxLength(60)]
        public string? BudgetRange { get; set; }
    }
}
