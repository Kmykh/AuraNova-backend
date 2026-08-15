using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Quotes.DTOs
{
    public class UpdateQuoteRequest
    {
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "El costo de envío debe ser mayor o igual a 0.")]
        public decimal ShippingCost { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }
}
