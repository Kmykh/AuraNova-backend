using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Payments.DTOs
{
    public class RejectPaymentRequest
    {
        [Required(ErrorMessage = "Debe proporcionar un motivo de rechazo.")]
        [MaxLength(2000)]
        public string Notes { get; set; } = null!;
    }
}
