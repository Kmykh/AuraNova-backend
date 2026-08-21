using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AuraNova.Domain.Enums;

namespace AuraNova.Domain.Entities
{
    public class Order
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = null!; // Unique, human-friendly
        public string TrackingToken { get; set; } = null!; // Cryptographically random, unique

        public Guid CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public DeliveryType DeliveryType { get; set; }

        public Guid? DeliveryZoneId { get; set; }
        public DeliveryZone? DeliveryZone { get; set; }

        public Guid? MeetingPointId { get; set; }
        public MeetingPoint? MeetingPoint { get; set; }

        public string? DeliveryAddress { get; set; }
        public string? Department { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }

        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal? Total { get; set; }

        public OrderStatus Status { get; set; }
        public string? Notes { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<OrderItem>? Items { get; set; }
        public Quote? Quote { get; set; }
        public Payment? Payment { get; set; }
        public ICollection<OrderStatusHistory>? StatusHistory { get; set; }
        public ICollection<Notification>? Notifications { get; set; }

        public Order()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            Status = OrderStatus.WaitingQuote;
            TrackingToken = GenerateTrackingToken();
        }

        /// <summary>
        /// Generates a cryptographically secure, URL-safe tracking token (32 bytes → Base64Url).
        /// </summary>
        private static string GenerateTrackingToken()
        {
            // Generates an 8-character token formatted as XXXX-XXXX
            var token = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            return $"{token.Substring(0, 4)}-{token.Substring(4, 4)}";
        }
    }
}