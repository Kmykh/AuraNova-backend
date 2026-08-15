using System;

namespace AuraNova.Application.Notifications.DTOs
{
    public class NotificationResponse
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Channel { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string Recipient { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string? ChannelUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
