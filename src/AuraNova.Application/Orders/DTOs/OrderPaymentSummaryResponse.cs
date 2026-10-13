namespace AuraNova.Application.Orders.DTOs
{
    /// <summary>
    /// Lo que necesita la página de pago, leído siempre de la BD (no del navegador):
    /// estado, montos, cotización y productos. No incluye datos personales del cliente.
    /// </summary>
    public class OrderPaymentSummaryResponse
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string StatusLabel { get; set; } = null!;
        public string DeliveryType { get; set; } = null!;
        public string? DeliveryPlace { get; set; }
        public bool IsCustomOrder { get; set; }

        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal CustomizationCost { get; set; }
        public decimal? Total { get; set; }

        public string? QuoteNotes { get; set; }
        public DateTimeOffset? QuotedAt { get; set; }

        public string? PaymentStatus { get; set; }
        public decimal? PaymentAmount { get; set; }
        /// <summary>Motivo del último rechazo, para que el cliente sepa qué corregir.</summary>
        public string? PaymentRejectionReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public List<PublicTrackingItemResponse> Items { get; set; } = [];
    }
}
