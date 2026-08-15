using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.DTOs;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Notifications;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class TrackingUrlServiceTests
    {
        [Fact]
        public async Task GenerateTrackingUrlAsync_ValidOrder_ReturnsCorrectUrl()
        {
            var mockSettings = new Mock<IBusinessSettingsService>();
            mockSettings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { TrackingBaseUrl = "https://auranova.pe/seguimiento/" });
            var service = new TrackingUrlService(mockSettings.Object);

            var order = new Order
            {
                OrderCode = "PED-2026-000001",
                TrackingToken = "token123"
            };

            var result = await service.GenerateTrackingUrlAsync(order);

            Assert.Equal("https://auranova.pe/seguimiento/PED-2026-000001/token123", result);
        }

        [Fact]
        public async Task GenerateTrackingUrlAsync_ShouldEncodeParameters()
        {
            var mockSettings = new Mock<IBusinessSettingsService>();
            mockSettings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { TrackingBaseUrl = "https://auranova.pe/seguimiento" });
            var service = new TrackingUrlService(mockSettings.Object);

            var order = new Order
            {
                OrderCode = "PED 2026",
                TrackingToken = "token 123"
            };

            var result = await service.GenerateTrackingUrlAsync(order);

            Assert.Equal("https://auranova.pe/seguimiento/PED%202026/token%20123", result);
        }

        [Fact]
        public async Task GenerateTrackingUrlAsync_MissingOrderCode_ThrowsException()
        {
            var mockSettings = new Mock<IBusinessSettingsService>();
            mockSettings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { TrackingBaseUrl = "https://auranova.pe/seguimiento" });
            var service = new TrackingUrlService(mockSettings.Object);

            var order = new Order
            {
                TrackingToken = "token456"
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateTrackingUrlAsync(order));
        }

        [Fact]
        public async Task GenerateTrackingUrlAsync_MissingToken_ThrowsException()
        {
            var mockSettings = new Mock<IBusinessSettingsService>();
            mockSettings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { TrackingBaseUrl = "https://auranova.pe/seguimiento" });
            var service = new TrackingUrlService(mockSettings.Object);

            var order = new Order
            {
                OrderCode = "PED-0003",
                TrackingToken = null!
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateTrackingUrlAsync(order));
        }
    }
}
