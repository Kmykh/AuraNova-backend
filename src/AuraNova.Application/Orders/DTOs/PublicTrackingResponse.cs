using System;
using System.Collections.Generic;

namespace AuraNova.Application.Orders.DTOs
{
    public class PublicTrackingResponse
    {
        /// <summary>
        /// Id interno: lo necesita el cliente para subir el comprobante o aceptar la cotización desde el seguimiento.
        /// Solo lo recibe quien tiene el código de seguimiento.
        /// </summary>
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string? CustomerFirstName { get; set; }
        public string Status { get; set; } = null!;
        public string StatusLabel { get; set; } = null!;
        public string DeliveryType { get; set; } = null!;
        
        public bool IsCustomOrder { get; set; }
        public string? ReferenceImageUrl { get; set; }
        public string? CustomizationNotes { get; set; }
        public string? Notes { get; set; }

        // Custom order brief (signed URLs for the images)
        public List<string> ReferenceImages { get; set; } = new();
        public List<string> ReferenceLinks { get; set; } = new();
        public DateOnly? DesiredDate { get; set; }
        public string? Occasion { get; set; }
        public string? BudgetRange { get; set; }

        public PublicTrackingCosts Costs { get; set; } = new();
        public PublicTrackingEstimates Estimates { get; set; } = new();
        public PublicTrackingNationalShipping? NationalShippingDetails { get; set; }

        public List<TrackingTimelineItem> Timeline { get; set; } = [];
        public PublicTrackingDeliveryResponse? Delivery { get; set; }
        public List<PublicTrackingItemResponse> Items { get; set; } = [];
    }

    public class PublicTrackingCosts
    {
        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal CustomizationCost { get; set; }
        public decimal? Total { get; set; }
    }

    public class PublicTrackingEstimates
    {
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EstimatedReadyAt { get; set; }
    }

    public class PublicTrackingNationalShipping
    {
        public string? Provider { get; set; }
        public string? TrackingCode { get; set; }
        public string? ProofUrl { get; set; }
    }

    public class PublicTrackingDeliveryResponse
    {
        public string? DeliveryZoneName { get; set; }
        public string? MeetingPointName { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Department { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
    }

    public class PublicTrackingItemResponse
    {
        public string ProductName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class TrackingTimelineItem
    {
        public string Status { get; set; } = null!;
        public string Label { get; set; } = null!;
        public string? Description { get; set; }
        public bool Completed { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }
}
