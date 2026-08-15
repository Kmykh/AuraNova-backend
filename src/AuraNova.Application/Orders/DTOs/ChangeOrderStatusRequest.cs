using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class ChangeOrderStatusRequest
    {
        [Required(ErrorMessage = "El estado es obligatorio.")]
        public string Status { get; set; } = null!;

        [MaxLength(2000)]
        public string? Comment { get; set; }
    }
}
