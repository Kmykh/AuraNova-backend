using System;

namespace AuraNova.Application.Notifications.DTOs
{
    public class WhatsAppPreparationResponse
    {
        public Guid NotificationId { get; set; }
        public string Status { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string WhatsappUrl { get; set; } = null!;
    }
}
