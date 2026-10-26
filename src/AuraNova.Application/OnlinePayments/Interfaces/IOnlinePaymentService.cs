using AuraNova.Application.OnlinePayments.DTOs;

namespace AuraNova.Application.OnlinePayments.Interfaces
{
    public interface IOnlinePaymentService
    {
        /// <summary>Cobra con Mercado Pago el adelanto, el total o el saldo del pedido.</summary>
        Task<OnlinePaymentResult> PayAsync(Guid orderId, CreateOnlinePaymentRequest request, CancellationToken cancellationToken = default);

        /// <summary>Webhook: vuelve a consultar la order en Mercado Pago y actualiza el pago (idempotente).</summary>
        Task HandleOrderNotificationAsync(string mercadoPagoOrderId, CancellationToken cancellationToken = default);
    }
}
