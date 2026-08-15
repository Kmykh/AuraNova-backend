using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateOrderCustomerRequest
    {
        [Required(ErrorMessage = "El nombre del cliente es requerido.")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "El teléfono del cliente es requerido.")]
        public string Phone { get; set; } = null!;

        public string? Email { get; set; }
    }
}
