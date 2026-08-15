using System;

namespace AuraNova.Application.Orders.DTOs
{
    public class OrderStatusChangeResponse
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string StatusLabel { get; set; } = null!;
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
