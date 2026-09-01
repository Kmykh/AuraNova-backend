namespace AuraNova.Application.Orders.DTOs
{
    public class CreateOrderResponse
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = null!;
        public string DeliveryType { get; set; } = null!;
        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal? Total { get; set; }
        public string Status { get; set; } = null!;
        public string TrackingToken { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public List<CreateOrderItemResponse> Items { get; set; } = [];
        public CreateOrderDeliveryResponse? Delivery { get; set; }
    }

    public class CreateOrderItemResponse
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }

        public string? SelectedPrimaryColor { get; set; }
        public string? SelectedSecondaryColor { get; set; }
        public string? SelectedFlowerType { get; set; }
        public string? SelectedFlowerColor { get; set; }
        public bool HasLights { get; set; }
        public bool HasButterfly { get; set; }
        public bool HasPhraseCard { get; set; }
        public string? PhraseText { get; set; }
        public string? PhraseFont { get; set; }
    }

    public class CreateOrderDeliveryResponse
    {
        public string? DeliveryZoneName { get; set; }
        public string? MeetingPointName { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Department { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
    }
}
