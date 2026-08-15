using System;
using AuraNova.Domain.Enums;

namespace AuraNova.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public NotificationType Type { get; set; }
        public NotificationChannel Channel { get; set; }
        public NotificationStatus Status { get; set; }

        public string Recipient { get; set; } = null!;
        public string? Subject { get; set; }
        public string Message { get; set; } = null!;
        public string? ChannelUrl { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public string? ErrorMessage { get; set; }

        public Notification()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            Status = NotificationStatus.Generated;
            Channel = NotificationChannel.WhatsApp;
        }
    }
}
