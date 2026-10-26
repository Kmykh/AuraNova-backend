using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.BusinessSettings.DTOs;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Notifications;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AuraNova.UnitTests
{
    /// <summary>Con solo el adelanto pagado, el pedido se prepara pero no sale hasta pagar el saldo.</summary>
    public class BalanceBeforeHandoverTests
    {
        private static AppDbContext GetDb() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static async Task<Order> SeedReadyOrder(AppDbContext db, DeliveryType deliveryType, decimal paid, decimal total = 100m)
        {
            var order = new Order
            {
                OrderCode = "AN-1001",
                Status = OrderStatus.Ready,
                DeliveryType = deliveryType,
                Subtotal = total,
                Total = total,
                Customer = new Customer { Name = "María", Phone = "987654321" }
            };
            db.Orders.Add(order);
            db.Payments.Add(new Payment { OrderId = order.Id, Amount = total, AmountPaid = paid, Status = PaymentStatus.Confirmed });
            await db.SaveChangesAsync();
            return order;
        }

        private static OrderStatusService StatusService(AppDbContext db) =>
            new(db, new OrderStatusTransitionService(), new Mock<INotificationService>().Object,
                new Mock<ICurrentUserService>().Object, new NullLogger<OrderStatusService>());

        [Theory]
        [InlineData(DeliveryType.Delivery, OrderStatus.Shipped)]
        [InlineData(DeliveryType.MeetingPoint, OrderStatus.Delivered)]
        public async Task WithBalance_OrderCannotLeave(DeliveryType deliveryType, OrderStatus handover)
        {
            using var db = GetDb();
            var order = await SeedReadyOrder(db, deliveryType, paid: 50m);

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                StatusService(db).ChangeStatusAsync(order.Id, handover, null));

            Assert.Contains("S/ 50.00", ex.Message);
            Assert.Equal(OrderStatus.Ready, (await db.Orders.FindAsync(order.Id))!.Status);
        }

        [Theory]
        [InlineData(DeliveryType.Delivery, OrderStatus.Shipped)]
        [InlineData(DeliveryType.MeetingPoint, OrderStatus.Delivered)]
        public async Task FullyPaid_OrderCanLeave(DeliveryType deliveryType, OrderStatus handover)
        {
            using var db = GetDb();
            var order = await SeedReadyOrder(db, deliveryType, paid: 100m);

            await StatusService(db).ChangeStatusAsync(order.Id, handover, null);

            Assert.Equal(handover, (await db.Orders.FindAsync(order.Id))!.Status);
        }

        [Fact]
        public async Task WithBalance_CannotBeHandedToAgency()
        {
            using var db = GetDb();
            var order = await SeedReadyOrder(db, DeliveryType.NationalShipping, paid: 50m);
            var service = new OrderService(db, new Mock<INotificationService>().Object, new NullLogger<OrderService>(),
                new OrderStatusTransitionService(), new Mock<ICurrentUserService>().Object, new Mock<IProductPriceResolver>().Object);

            await Assert.ThrowsAsync<OrderValidationException>(() => service.DeliverToAgencyAsync(order.Id, "Shalom", "123", null));
            Assert.Equal(OrderStatus.Ready, (await db.Orders.FindAsync(order.Id))!.Status);
        }

        [Fact]
        public async Task MarkAsReady_SendsReadyNotification()
        {
            using var db = GetDb();
            var order = await SeedReadyOrder(db, DeliveryType.Delivery, paid: 50m);
            order.Status = OrderStatus.Preparing;
            await db.SaveChangesAsync();
            var notifications = new Mock<INotificationService>();
            var service = new OrderService(db, notifications.Object, new NullLogger<OrderService>(),
                new OrderStatusTransitionService(), new Mock<ICurrentUserService>().Object, new Mock<IProductPriceResolver>().Object);

            await service.MarkAsReadyAsync(order.Id);

            notifications.Verify(n => n.NotifyAsync(order.Id, NotificationType.OrderReady, null), Times.Once);
        }

        [Fact]
        public async Task ReadyMessage_AsksForBalanceWithPaymentLink()
        {
            var settings = new Mock<IBusinessSettingsService>();
            settings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { BusinessName = "Aura Nova" });
            var templates = new NotificationTemplateService(settings.Object);

            var order = new Order
            {
                OrderCode = "AN-1001",
                DeliveryType = DeliveryType.NationalShipping,
                Customer = new Customer { Name = "María" },
                Payment = new Payment { Amount = 100m, AmountPaid = 50m }
            };

            var withBalance = await templates.BuildReadyMessageAsync(order);
            Assert.Contains("agencia", withBalance);
            Assert.Contains("saldo de S/ 50.00", withBalance);
            Assert.Contains($"/pago/{order.Id}", withBalance);

            order.Payment.AmountPaid = 100m;
            var paid = await templates.BuildReadyMessageAsync(order);
            Assert.DoesNotContain("saldo", paid);
        }
    }
}
