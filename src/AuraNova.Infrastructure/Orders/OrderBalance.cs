using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>
    /// Regla del negocio: con el adelanto del 50% el pedido se prepara, pero no sale (delivery, punto de encuentro
    /// o agencia) hasta que el cliente paga el saldo.
    /// </summary>
    public static class OrderBalance
    {
        /// <summary>Estados que significan que el pedido ya salió o se entregó.</summary>
        public static bool IsHandover(OrderStatus status) =>
            status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.DeliveredToAgency;

        /// <summary>Saldo por cobrar (0 si no hay pago registrado o ya está completo).</summary>
        public static decimal Due(Order order) =>
            order.Payment == null ? 0m : Math.Max(0m, order.Payment.Amount - order.Payment.AmountPaid);

        public static void EnsurePaidBeforeHandover(Order order, OrderStatus newStatus)
        {
            var due = Due(order);
            if (IsHandover(newStatus) && due > 0)
                throw new OrderValidationException(
                    $"El cliente aún debe el saldo de S/ {due:F2}. El pedido se puede entregar cuando lo pague desde su enlace de pago.");
        }
    }
}
