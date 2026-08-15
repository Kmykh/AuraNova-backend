using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.WhatsApp.Interfaces;

namespace AuraNova.Infrastructure.WhatsApp
{
    public class WhatsAppMessageService : IWhatsAppMessageService
    {
        private readonly IBusinessSettingsService _settingsService;

        public WhatsAppMessageService(IBusinessSettingsService settingsService)
        {
            _settingsService = settingsService;
        }
        public string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            // Remove non-digit characters
            var digits = new string(phone.Where(char.IsDigit).ToArray());

            // If phone starts with "9" and has 9 digits, prepend Peru code "51"
            if (digits.Length == 9 && digits.StartsWith('9'))
                digits = "51" + digits;

            return digits;
        }

        public async Task<string> GenerateUrlAsync(string message)
        {
            var settings = await _settingsService.GetPublicAsync();
            var number = settings.WhatsAppNumber;
            if (string.IsNullOrWhiteSpace(number))
                return string.Empty;

            var encodedMessage = Uri.EscapeDataString(message);
            return $"https://wa.me/{number}?text={encodedMessage}";
        }
    }
}
