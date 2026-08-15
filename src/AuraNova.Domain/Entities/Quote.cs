using System;
using AuraNova.Domain.Enums;

namespace AuraNova.Domain.Entities
{
    public class Quote
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public decimal? ShippingCost { get; set; }
        public string? Notes { get; set; }
        public QuoteStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? QuotedAt { get; set; }

        public Quote()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            Status = QuoteStatus.Pending;
        }
    }
}