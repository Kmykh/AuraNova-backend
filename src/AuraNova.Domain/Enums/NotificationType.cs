namespace AuraNova.Domain.Enums
{
    public enum NotificationType
    {
        OrderCreated,
        QuoteReady,
        PaymentReported,
        PaymentConfirmed,
        PaymentRejected,
        OrderPreparing,
        OrderReady,
        OrderShipped,
        OrderDelivered,
        OrderCancelled,
        /// <summary>El cliente pagó el saldo (después del adelanto): el pedido queda pagado por completo.</summary>
        BalancePaid
    }
}
