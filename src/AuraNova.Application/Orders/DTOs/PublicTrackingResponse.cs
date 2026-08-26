using System;
using System.Collections.Generic;

namespace AuraNova.Application.Orders.DTOs
{
    public class PublicTrackingResponse
    {
        public string OrderCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string StatusLabel { get; set; } = null!;
        public string DeliveryType { get; set; } = null!;
        public decimal? Total { get; set; }
        public List<TrackingTimelineItem> Timeline { get; set; } = [];
        public PublicTrackingDeliveryResponse? Delivery { get; set; }
        public List<PublicTrackingItemResponse> Items { get; set; } = [];
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
    }

    public class TrackingTimelineItem
    {
        public string Status { get; set; } = null!;
        public string Label { get; set; } = null!;
        public bool Completed { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }
}
