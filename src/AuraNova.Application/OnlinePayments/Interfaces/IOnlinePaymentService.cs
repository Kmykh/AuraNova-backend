using AuraNova.Application.OnlinePayments.DTOs;

namespace AuraNova.Application.OnlinePayments.Interfaces
{
    public interface IOnlinePaymentService
    {
        /// <summary>Cobra con Mercado Pago el adelanto, el total o el saldo del pedido.</summary>
        Task<OnlinePaymentResult> PayAsync(Guid orderId, CreateOnlinePaymentRequest request, CancellationToken cancellationToken = default);

        /// <summary>Webhook: vuelve a consultar la order en Mercado Pago y actualiza el pago (idempotente).</summary>
        Task HandleOrderNotificationAsync(string mercadoPagoOrderId, CancellationToken cancellationToken = default);

        /// <summary>Devuelve un cobro aprobado (total o parcial) y lo descuenta de lo pagado.</summary>
        Task<RefundOnlinePaymentResult> RefundAsync(Guid orderId, Guid transactionId, RefundOnlinePaymentRequest request, CancellationToken cancellationToken = default);

        /// <summary>Al cancelar un pedido: anula en Mercado Pago los cobros que aún esperan una acción del cliente.</summary>
        Task CancelPendingAsync(Guid orderId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Respaldo de los webhooks: consulta en Mercado Pago los cobros que siguen sin resultado y los actualiza.
        /// Devuelve cuántos cambiaron de estado.
        /// </summary>
        Task<int> ReconcilePendingAsync(CancellationToken cancellationToken = default);

        Task LogWebhookAsync(string? topic, string? resourceId, string result, string? detail, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PaymentWebhookEventResponse>> GetWebhookEventsAsync(int take, CancellationToken cancellationToken = default);

        Task<OnlinePaymentReport> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    }
}
