using System;

namespace AuraNova.Application.BusinessSettings.DTOs
{
    public class BusinessSettingsResponse
    {
        public Guid Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string WhatsAppNumber { get; set; } = string.Empty;
        public string YapeHolderName { get; set; } = string.Empty;
        public string? YapeQrImageUrl { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
