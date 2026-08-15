using System;
using AuraNova.Domain.Enums;

namespace AuraNova.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public PaymentMethod Method { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }

        public string? EvidenceUrl { get; set; }
        public string? Notes { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }

        public Payment()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            Method = PaymentMethod.Yape; // Only Yape for now
            Status = PaymentStatus.Pending;
        }
    }
}
