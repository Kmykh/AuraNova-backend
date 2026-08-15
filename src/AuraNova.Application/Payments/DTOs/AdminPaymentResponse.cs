namespace AuraNova.Application.Payments.DTOs
{
    public class AdminPaymentResponse : PaymentResponse
    {
        public string CustomerName { get; set; } = null!;
        public string CustomerPhone { get; set; } = null!;
    }
}
