using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateOrderItemRequest
    {
        [Required] public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Quantity { get; set; }
    }
}
