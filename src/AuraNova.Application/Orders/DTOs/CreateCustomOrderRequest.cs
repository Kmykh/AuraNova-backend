using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateCustomOrderRequest
    {
        [Required]
        public CreateOrderCustomerRequest Customer { get; set; } = null!;

        [Required]
        public CreateOrderDeliveryRequest Delivery { get; set; } = null!;

        public string? ReferenceImageUrl { get; set; }
        
        [MaxLength(2000)]
        public string? CustomizationNotes { get; set; }
    }
}
