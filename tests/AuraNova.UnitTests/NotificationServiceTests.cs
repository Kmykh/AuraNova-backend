using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.WhatsApp.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Notifications;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class NotificationServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private async Task<Order> SeedOrder(AppDbContext db)
        {
            var customer = new Customer { Name = "Test", Phone = "999999999" };
            db.Customers.Add(customer);

            var order = new Order
            {
                OrderCode = "PED-2026-000001",
                CustomerId = customer.Id,
                Customer = customer
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            return order;
        }

        [Fact]
        public async Task NotifyAsync_ShouldCreateNotificationInDatabase()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrder(db);

            var templateService = new Mock<INotificationTemplateService>();
            templateService.Setup(t => t.BuildOrderCreatedMessageAsync(It.IsAny<Order>())).ReturnsAsync("Created message");
            templateService.Setup(t => t.BuildPreparingMessageAsync(It.IsAny<Order>())).ReturnsAsync("Preparing message");

            var whatsAppService = new Mock<IWhatsAppMessageService>();
            whatsAppService.Setup(s => s.NormalizePhone(It.IsAny<string>())).Returns("51999999999");
            whatsAppService.Setup(s => s.GenerateUrlAsync(It.IsAny<string>())).ReturnsAsync("https://wa.me/test");

            var service = new NotificationService(db, templateService.Object, whatsAppService.Object, new NullLogger<NotificationService>());

            await service.NotifyAsync(order.Id, NotificationType.OrderPreparing);

            var dbNotification = await db.Notifications.FirstOrDefaultAsync();

            Assert.NotNull(dbNotification);
            Assert.Equal(NotificationType.OrderPreparing, dbNotification.Type);
            Assert.Equal("Preparing message", dbNotification.Message);
            Assert.Equal("https://wa.me/test", dbNotification.ChannelUrl);
            Assert.Equal("51999999999", dbNotification.Recipient);
            Assert.Equal(NotificationStatus.Generated, dbNotification.Status);
        }

        [Fact]
        public async Task NotifyAsync_ShouldNotThrowIfOrderNotFound_SwallowsException()
        {
            using var db = GetInMemoryDb();
            var templateService = new Mock<INotificationTemplateService>();
            var whatsAppService = new Mock<IWhatsAppMessageService>();
            var service = new NotificationService(db, templateService.Object, whatsAppService.Object, new NullLogger<NotificationService>());

            // Should complete without throwing exceptions
            await service.NotifyAsync(Guid.NewGuid(), NotificationType.OrderCreated);

            Assert.Empty(db.Notifications);
        }

        [Fact]
        public async Task NotifyAsync_ShouldNotCreateDuplicateNotificationWithin5Minutes()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrder(db);

            db.Notifications.Add(new Notification
            {
                OrderId = order.Id,
                Type = NotificationType.PaymentConfirmed,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                Message = "test",
                Recipient = "test"
            });
            await db.SaveChangesAsync();

            var templateService = new Mock<INotificationTemplateService>();
            var whatsAppService = new Mock<IWhatsAppMessageService>();
            var service = new NotificationService(db, templateService.Object, whatsAppService.Object, new NullLogger<NotificationService>());

            await service.NotifyAsync(order.Id, NotificationType.PaymentConfirmed);

            var count = await db.Notifications.CountAsync();
            Assert.Equal(1, count); // Duplicate was skipped
        }
    }
}
