using AuraNova.Domain.Enums;

namespace AuraNova.Application.Orders
{
    public static class OrderStatusLabels
    {
        private static readonly Dictionary<OrderStatus, string> Labels = new()
        {
            [OrderStatus.WaitingQuote]     = "Cotizando envío",
            [OrderStatus.QuoteReady]       = "Cotización lista",
            [OrderStatus.WaitingPayment]   = "Pendiente de pago",
            [OrderStatus.PaymentReported]  = "Pago en revisión",
            [OrderStatus.PaymentConfirmed] = "Pago confirmado",
            [OrderStatus.Preparing]        = "Preparando tu pedido",
            [OrderStatus.Ready]            = "Listo para entregar",
            [OrderStatus.Shipped]          = "Enviado",
            [OrderStatus.Delivered]        = "Entregado",
            [OrderStatus.Cancelled]        = "Pedido cancelado",
        };

        public static string GetLabel(OrderStatus status)
        {
            return Labels.TryGetValue(status, out var label) ? label : status.ToString();
        }
    }
}
