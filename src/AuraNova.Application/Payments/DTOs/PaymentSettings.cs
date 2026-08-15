namespace AuraNova.Application.Payments.DTOs
{
    public class PaymentSettings
    {
        public bool YapeEnabled { get; set; } = true;
        public string YapeHolderName { get; set; } = null!;
        public string YapeQrImageUrl { get; set; } = null!;
    }
}
