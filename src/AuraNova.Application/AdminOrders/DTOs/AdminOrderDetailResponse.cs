using System;
using System.Collections.Generic;

namespace AuraNova.Application.AdminOrders.DTOs
{
    public class AdminOrderDetailResponse
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DeliveryType { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal? Total { get; set; }
        public bool HasPaymentEvidence { get; set; }

        public AdminOrderCustomer Customer { get; set; } = new();
        public AdminOrderDelivery Delivery { get; set; } = new();
        public AdminOrderQuote? Quote { get; set; }
        public AdminOrderPayment? Payment { get; set; }
        
        public List<AdminOrderItem> Items { get; set; } = new();
        public List<AdminOrderStatusHistory> StatusHistory { get; set; } = new();
        public List<AdminOrderNotification> Notifications { get; set; } = new();
    }

    public class AdminOrderCustomer
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class AdminOrderDelivery
    {
        public string? DeliveryZone { get; set; }
        public string? MeetingPoint { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Department { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
    }

    public class AdminOrderQuote
    {
        public string QuoteStatus { get; set; } = string.Empty;
        public decimal ShippingCost { get; set; }
        public string? Notes { get; set; }
        public DateTimeOffset? QuotedAt { get; set; }
    }

    public class AdminOrderPayment
    {
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }
    }

    public class AdminOrderItem
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class AdminOrderStatusHistory
    {
        public string Status { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class AdminOrderNotification
    {
        public string Type { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
