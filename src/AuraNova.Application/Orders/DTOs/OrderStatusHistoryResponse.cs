using System;

namespace AuraNova.Application.Orders.DTOs
{
    public class OrderStatusHistoryResponse
    {
        public string Status { get; set; } = null!;
        public string Label { get; set; } = null!;
        public string? Comment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
