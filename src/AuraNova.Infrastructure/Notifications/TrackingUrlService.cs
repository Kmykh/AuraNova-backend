using System;
using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Notifications
{
    public class TrackingUrlService : ITrackingUrlService
    {
        private readonly IBusinessSettingsService _settingsService;

        public TrackingUrlService(IBusinessSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<string> GenerateTrackingUrlAsync(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.OrderCode))
                throw new ArgumentException("OrderCode is missing.");
            if (string.IsNullOrWhiteSpace(order.TrackingToken))
                throw new ArgumentException("TrackingToken is missing.");

            var settings = await _settingsService.GetPublicAsync();
            var baseUrl = settings.TrackingBaseUrl.TrimEnd('/');
            
            // Encode parameters just in case
            var code = Uri.EscapeDataString(order.OrderCode);
            var token = Uri.EscapeDataString(order.TrackingToken);

            return $"{baseUrl}/{code}/{token}";
        }
    }
}
