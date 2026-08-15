namespace AuraNova.Application.BusinessSettings.DTOs
{
    public class UpdateBusinessSettingsRequest
    {
        public string BusinessName { get; set; } = string.Empty;
        public string WhatsAppNumber { get; set; } = string.Empty;
        public string YapeHolderName { get; set; } = string.Empty;
        public string TrackingBaseUrl { get; set; } = string.Empty;
    }
}
