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

        // Quién recibe (regalos) y datos que piden las agencias en envíos nacionales
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        /// <summary>DNI o CE de quien recoge en la agencia.</summary>
        public string? RecipientDocument { get; set; }
        /// <summary>Referencia de la dirección (color de la casa, piso, frente a…).</summary>
        public string? DeliveryReference { get; set; }
        /// <summary>Agencia que prefiere el cliente para envíos nacionales (Shalom, Olva).</summary>
        public string? PreferredAgency { get; set; }

        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal? Total { get; set; }

        public OrderStatus Status { get; set; }
        public string? Notes { get; set; }

        // Custom Order Fields
        public bool IsCustomOrder { get; set; } = false;
        /// <summary>Primera imagen de referencia (compatibilidad); todas están en <see cref="ReferenceImages"/>.</summary>
        public string? ReferenceImageUrl { get; set; }
        public string? CustomizationNotes { get; set; }
        public decimal CustomizationCost { get; set; }

        // Brief of the custom order: what the customer wants and when
        public ICollection<OrderReferenceImage>? ReferenceImages { get; set; }
        public List<string> ReferenceLinks { get; set; } = new List<string>();
        public DateOnly? DesiredDate { get; set; }
        public string? Occasion { get; set; }
        public string? BudgetRange { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        
        // Timing
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EstimatedReadyAt { get; set; }
        public DateTimeOffset? ReadyAt { get; set; }
        public DateTimeOffset? DeliveredToAgencyAt { get; set; }

        // Shipping (NationalShipping details)
        public string? ShippingProvider { get; set; }
        public string? ShippingTrackingCode { get; set; }
        public string? ShippingProofUrl { get; set; }

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
            TrackingToken = OrderCodes.NewTrackingCode();
        }
    }
}