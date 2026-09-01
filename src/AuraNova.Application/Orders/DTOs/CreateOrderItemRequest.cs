using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateOrderItemRequest
    {
        [Required] public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Quantity { get; set; }

        public string? SelectedPrimaryColor { get; set; }
        public string? SelectedSecondaryColor { get; set; }
        public string? SelectedFlowerType { get; set; }
        public string? SelectedFlowerColor { get; set; }
        public bool HasLights { get; set; }
        public bool HasButterfly { get; set; }
        public bool HasPhraseCard { get; set; }
        public string? PhraseText { get; set; }
        public string? PhraseFont { get; set; }
    }
}
