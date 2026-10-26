using System;

namespace AuraNova.Domain.Entities
{
    /// <summary>
    /// Un cobro en línea de un pedido (Mercado Pago, con tarjeta o Yape). Un pedido puede tener dos:
    /// el adelanto del 50% y luego el saldo. Solo los aprobados (<see cref="ApprovedAt"/>) suman a lo pagado.
    /// </summary>
    public class PaymentTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PaymentId { get; set; }
        public Payment? Payment { get; set; }
        public Guid OrderId { get; set; }

        public string Provider { get; set; } = "MercadoPago";
        /// <summary>Id de la order en Mercado Pago ("ORD…").</summary>
        public string? ExternalOrderId { get; set; }
        /// <summary>Id del pago dentro de la order de Mercado Pago ("PAY…").</summary>
        public string? ExternalPaymentId { get; set; }

        /// <summary>visa, master, yape…</summary>
        public string MethodId { get; set; } = null!;
        /// <summary>credit_card, debit_card…</summary>
        public string? MethodType { get; set; }

        public decimal Amount { get; set; }
        /// <summary>true si es el adelanto del 50%.</summary>
        public bool IsDeposit { get; set; }

        /// <summary>Estado según Mercado Pago (processed, failed, processing, action_required…) o "created" antes de enviarlo.</summary>
        public string Status { get; set; } = "created";
        public string? StatusDetail { get; set; }

        /// <summary>Lo devuelto al cliente desde el panel (total o parcial). Se descuenta de lo pagado.</summary>
        public decimal RefundedAmount { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public DateTimeOffset? RefundedAt { get; set; }
    }
}
