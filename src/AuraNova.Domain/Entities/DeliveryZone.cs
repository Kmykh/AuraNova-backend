using System;
using System.Collections.Generic;

namespace AuraNova.Domain.Entities
{
    public class DeliveryZone
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string District { get; set; } = null!;
        public decimal Cost { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<Order>? Orders { get; set; }

        public DeliveryZone()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            IsActive = true;
        }
    }
}