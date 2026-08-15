namespace AuraNova.Application.Payments.DTOs
{
    public class PaymentInfoResponse
    {
        public bool Enabled { get; set; }
        public string Method { get; set; } = null!;
        public string HolderName { get; set; } = null!;
        public string QrImageUrl { get; set; } = null!;
    }
}
