namespace AuraNova.Domain.Enums
{
    public enum OrderStatus
    {
        WaitingQuote,
        QuoteReady,
        WaitingPayment,
        PaymentReported,
        PaymentConfirmed,
        Preparing,
        Ready,
        Shipped,
        Delivered,
        Cancelled
    }
}