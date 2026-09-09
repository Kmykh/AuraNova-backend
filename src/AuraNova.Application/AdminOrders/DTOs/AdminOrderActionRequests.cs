using System;

namespace AuraNova.Application.AdminOrders.DTOs
{
    public class SetEstimatedReadyDateRequest
    {
        public DateTimeOffset EstimatedDate { get; set; }
    }

    public class DeliverToAgencyRequest
    {
        public string Provider { get; set; } = string.Empty;
        public string TrackingCode { get; set; } = string.Empty;
        public string? ProofUrl { get; set; }
    }
}
