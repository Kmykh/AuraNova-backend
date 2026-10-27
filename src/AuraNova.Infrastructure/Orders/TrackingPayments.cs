using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>El dinero de un pedido explicado para el cliente: qué pagó, qué se le devolvió y con qué medio.</summary>
    public static class TrackingPayments
    {
        /// <summary>Un cobro sin confirmar se muestra "en proceso" solo un rato: después se da por no hecho.</summary>
        private static readonly TimeSpan ProcessingWindow = TimeSpan.FromMinutes(30);

        public static PublicTrackingPayment Build(Order order, DateTimeOffset now)
        {
            var payment = order.Payment;
            var transactions = payment?.Transactions ?? [];
            var approved = transactions.Where(t => t.ApprovedAt != null).OrderBy(t => t.ApprovedAt).ToList();

            var movements = new List<PublicTrackingMoneyMovement>();
            foreach (var t in approved)
            {
                movements.Add(new() { Kind = "payment", Amount = t.Amount, Method = MethodLabel(t), IsDeposit = t.IsDeposit, Date = t.ApprovedAt });
                if (t.RefundedAmount > 0)
                    movements.Add(new() { Kind = "refund", Amount = t.RefundedAmount, Method = MethodLabel(t), Date = t.RefundedAt ?? t.UpdatedAt });
            }

            var charged = approved.Sum(t => t.Amount);
            var refunded = approved.Sum(t => t.RefundedAmount);

            // Pagos del flujo anterior (Yape con comprobante): solo se sabe lo pagado
            if (approved.Count == 0 && payment is { AmountPaid: > 0 })
            {
                charged = payment.AmountPaid;
                movements.Add(new() { Kind = "payment", Amount = payment.AmountPaid, Method = payment.Method == PaymentMethod.Yape ? "Yape" : "Mercado Pago", Date = payment.VerifiedAt });
            }

            var total = order.Total ?? payment?.Amount ?? 0m;
            var net = Math.Max(0m, charged - refunded);
            var due = Math.Max(0m, (payment?.Amount ?? total) - net);
            var processing = transactions.Any(t => t.ApprovedAt == null && t.Status == "processing" && t.CreatedAt > now - ProcessingWindow);

            var state = order.Status == OrderStatus.Cancelled
                ? net > 0 ? "refund_pending" : refunded > 0 ? "refunded" : "none"
                : net > 0 && due <= 0 ? "paid"
                : net > 0 ? "deposit"
                : processing ? "processing"
                : "unpaid";

            return new PublicTrackingPayment
            {
                State = state,
                Total = total,
                Charged = charged,
                Refunded = refunded,
                NetPaid = net,
                BalanceDue = order.Status == OrderStatus.Cancelled ? 0m : due,
                Movements = movements.OrderBy(m => m.Date ?? DateTimeOffset.MaxValue).ToList()
            };
        }

        /// <summary>"Yape", "Visa", "Mastercard"… (Mercado Pago usa visa, debvisa, master, debmaster…).</summary>
        public static string MethodLabel(PaymentTransaction transaction)
        {
            var id = transaction.MethodId?.ToLowerInvariant() ?? "";
            if (id == "yape") return "Yape";
            if (id.Contains("visa")) return "Visa";
            if (id.Contains("master")) return "Mastercard";
            if (id.Contains("amex")) return "American Express";
            if (id.Contains("diners")) return "Diners Club";
            return "tarjeta";
        }
    }
}
