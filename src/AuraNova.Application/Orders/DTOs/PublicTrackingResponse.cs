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
    }

    public class TrackingTimelineItem
    {
        public string Status { get; set; } = null!;
        public string Label { get; set; } = null!;
        public bool Completed { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }
}
