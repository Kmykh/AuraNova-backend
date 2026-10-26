using System;
using System.Collections.Generic;
using AuraNova.Domain.Enums;

namespace AuraNova.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public PaymentMethod Method { get; set; }
        /// <summary>Total a cobrar por el pedido.</summary>
        public decimal Amount { get; set; }
        /// <summary>Lo ya cobrado (puede ser solo el adelanto del 50%). El saldo es Amount - AmountPaid.</summary>
        public decimal AmountPaid { get; set; }
        public PaymentStatus Status { get; set; }

        public string? EvidenceUrl { get; set; }
        public string? Notes { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }

        /// <summary>Cobros en línea (Mercado Pago).</summary>
        public ICollection<PaymentTransaction>? Transactions { get; set; }

        public Payment()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            Method = PaymentMethod.Yape; // Only Yape for now
            Status = PaymentStatus.Pending;
        }
    }
}
