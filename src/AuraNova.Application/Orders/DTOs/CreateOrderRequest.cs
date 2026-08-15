using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateOrderRequest
    {
        [Required]
        public CreateOrderCustomerRequest Customer { get; set; } = null!;

        [Required]
        [MinLength(1, ErrorMessage = "El pedido debe contener al menos un producto.")]
        public List<CreateOrderItemRequest> Items { get; set; } = [];

        [Required(ErrorMessage = "La información de entrega es obligatoria.")]
        public CreateOrderDeliveryRequest Delivery { get; set; } = null!;
    }
}
