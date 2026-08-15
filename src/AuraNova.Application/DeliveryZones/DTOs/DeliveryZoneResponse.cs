namespace AuraNova.Application.DeliveryZones.DTOs
{
    public class DeliveryZoneResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string District { get; set; } = null!;
        public decimal Cost { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
