using System;
using System.Collections.Generic;

namespace AuraNova.Domain.Entities
{
    public class Customer
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string? Email { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public ICollection<Order>? Orders { get; set; }

        public Customer()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}