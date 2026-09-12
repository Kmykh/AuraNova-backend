using System;

namespace AuraNova.Domain.Entities
{
    public class BusinessSettings
    {
        public Guid Id { get; set; }
        
        public string BusinessName { get; set; } = string.Empty;
        public string WhatsAppNumber { get; set; } = string.Empty;
        
        public string YapeHolderName { get; set; } = string.Empty;
        public string? YapeQrImageUrl { get; set; }
        public string? TikTokUsername { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public BusinessSettings()
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
