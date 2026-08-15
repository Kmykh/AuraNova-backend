using System;
using AuraNova.Domain.Enums;

namespace AuraNova.Domain.Entities
{
    public class OrderStatusHistory
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public OrderStatus Status { get; set; }
        public string? Comment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public OrderStatusHistory()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
