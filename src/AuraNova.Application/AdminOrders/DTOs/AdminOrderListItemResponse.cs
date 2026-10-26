using System;
using System.Collections.Generic;

namespace AuraNova.Application.AdminOrders.DTOs
{
    public class AdminOrderListItemResponse
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DeliveryType { get; set; } = string.Empty;
        public string TrackingToken { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal CustomizationCost { get; set; }
        public decimal? Total { get; set; }
        public bool IsCustomOrder { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EstimatedReadyAt { get; set; }

        // Qué pidió, sin abrir el detalle
        public int ItemCount { get; set; }
        public List<AdminOrderListProduct> Items { get; set; } = new List<AdminOrderListProduct>();
        public int ReferenceImageCount { get; set; }
        public string? PaymentStatus { get; set; }
        /// <summary>Saldo por cobrar cuando el cliente pagó solo el adelanto.</summary>
        public decimal? BalanceDue { get; set; }
        public DateOnly? DesiredDate { get; set; }
        public string? Occasion { get; set; }
        public string? DeliveryPlace { get; set; }
    }

    public class AdminOrderListProduct
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Colors { get; set; } = new List<string>();
        public bool HasExtras { get; set; }
    }

    /// <summary>Cantidad de pedidos por estado, para las pestañas del panel.</summary>
    public class AdminOrderCountsResponse
    {
        public Dictionary<string, int> ByStatus { get; set; } = new Dictionary<string, int>();
        public int Total { get; set; }
    }
}
