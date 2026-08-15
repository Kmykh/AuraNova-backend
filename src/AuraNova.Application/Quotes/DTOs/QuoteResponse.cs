namespace AuraNova.Application.Quotes.DTOs
{
    public class QuoteResponse
    {
        public Guid QuoteId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public decimal? ShippingCost { get; set; }
        public decimal Subtotal { get; set; }
        public decimal? Total { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = null!;
        public string OrderStatus { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? QuotedAt { get; set; }

        // Customer info
        public string CustomerName { get; set; } = null!;
        public string CustomerPhone { get; set; } = null!;
    }
}
