using System;
using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.DTOs;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Notifications;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class NotificationTemplateServiceTests
    {
        private Order BuildOrder(DeliveryType type)
        {
            return new Order
            {
                OrderCode = "PED-2026-000001",
                Customer = new Customer { Name = "María", Phone = "999999999" },
                DeliveryType = type,
                Total = 150.50m
            };
        }

        [Fact]
        public async Task BuildOrderCreatedMessageAsync_Delivery_ShouldContainYapeInstructions()
        {
            var urlService = new Mock<ITrackingUrlService>();
            urlService.Setup(s => s.GenerateTrackingUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://track.url");
            var settingsMock = new Mock<IBusinessSettingsService>();
            settingsMock.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { BusinessName = "Aura Nova" });

            var service = new NotificationTemplateService(urlService.Object, settingsMock.Object);
            var order = BuildOrder(DeliveryType.Delivery);

            var message = await service.BuildOrderCreatedMessageAsync(order);

            Assert.Contains("María", message);
            Assert.Contains("PED-2026-000001", message);
            Assert.Contains("150.50", message);
            Assert.Contains("https://track.url", message);
        }

        [Fact]
        public async Task BuildOrderCreatedMessageAsync_NationalShipping_ShouldContainQuoteInstructions()
        {
            var urlService = new Mock<ITrackingUrlService>();
            urlService.Setup(s => s.GenerateTrackingUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://track.url");
            var settingsMock = new Mock<IBusinessSettingsService>();
            settingsMock.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { BusinessName = "Aura Nova" });

            var service = new NotificationTemplateService(urlService.Object, settingsMock.Object);
            var order = BuildOrder(DeliveryType.NationalShipping);

            var message = await service.BuildOrderCreatedMessageAsync(order);

            Assert.Contains("María", message);
            Assert.Contains("PED-2026-000001", message);
            Assert.Contains("nivel nacional", message);
            Assert.Contains("https://track.url", message);
        }

        [Fact]
        public async Task BuildQuoteReadyMessageAsync_ShouldContainCosts()
        {
            var urlService = new Mock<ITrackingUrlService>();
            urlService.Setup(s => s.GenerateTrackingUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://track.url");
            var settingsMock = new Mock<IBusinessSettingsService>();
            settingsMock.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { BusinessName = "Aura Nova" });

            var service = new NotificationTemplateService(urlService.Object, settingsMock.Object);
            
            var order = BuildOrder(DeliveryType.NationalShipping);
            order.Subtotal = 150.50m;
            order.Quote = new Quote
            {
                ShippingCost = 20.00m
            };

            var message = await service.BuildQuoteReadyMessageAsync(order);

            Assert.Contains("150.50", message);
            Assert.Contains("20.00", message);
            Assert.Contains("170.50", message); // Total
            Assert.Contains("https://track.url", message);
        }

        [Fact]
        public async Task BuildPaymentRejectedMessageAsync_ShouldIncludeReason()
        {
            var urlService = new Mock<ITrackingUrlService>();
            urlService.Setup(s => s.GenerateTrackingUrlAsync(It.IsAny<Order>())).ReturnsAsync("https://track.url");
            var settingsMock = new Mock<IBusinessSettingsService>();
            settingsMock.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { BusinessName = "Aura Nova" });

            var service = new NotificationTemplateService(urlService.Object, settingsMock.Object);
            var order = BuildOrder(DeliveryType.Delivery);

            var message = await service.BuildPaymentRejectedMessageAsync(order, "El comprobante es de otra fecha.");

            Assert.Contains("El comprobante es de otra fecha", message);
        }
    }
}
