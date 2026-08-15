using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;


namespace AuraNova.UnitTests
{
    public class OrderStatusServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private async Task<Order> SeedOrder(AppDbContext db, OrderStatus status = OrderStatus.PaymentConfirmed,
            DeliveryType deliveryType = DeliveryType.Delivery)
        {
            var order = new Order
            {
                OrderCode = "PED-2026-000001",
                Status = status,
                DeliveryType = deliveryType,
                Subtotal = 80m,
                Total = 87m
            };
            db.Orders.Add(order);

            // Add initial history
            db.OrderStatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = status
            });

            await db.SaveChangesAsync();
            return order;
        }

        [Fact]
        public async Task ChangeStatusAsync_ValidTransition_ShouldUpdateOrderAndCreateHistory()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrder(db, OrderStatus.PaymentConfirmed);
            var transitions = new OrderStatusTransitionService();
            var notificationService = new Mock<INotificationService>();
            var service = new OrderStatusService(db, transitions, notificationService.Object, new NullLogger<OrderStatusService>());

            var result = await service.ChangeStatusAsync(order.Id, OrderStatus.Preparing, "Comenzamos a preparar.");

            Assert.Equal("Preparing", result.Status);
            Assert.Equal("Preparando tu pedido", result.StatusLabel);

            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.Equal(OrderStatus.Preparing, dbOrder!.Status);

            var history = await db.OrderStatusHistory.Where(h => h.OrderId == order.Id).OrderBy(h => h.CreatedAt).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.Equal(OrderStatus.Preparing, history[1].Status);
            Assert.Equal("Comenzamos a preparar.", history[1].Comment);
        }

        [Fact]
        public async Task ChangeStatusAsync_InvalidTransition_ShouldThrow()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrder(db, OrderStatus.WaitingPayment);
            var transitions = new OrderStatusTransitionService();
            var notificationService = new Mock<INotificationService>();
            var service = new OrderStatusService(db, transitions, notificationService.Object, new NullLogger<OrderStatusService>());

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                service.ChangeStatusAsync(order.Id, OrderStatus.Delivered, null));
            Assert.Contains("Transición inválida", ex.Message);
        }

        [Fact]
        public async Task ChangeStatusAsync_OrderNotFound_ShouldThrow()
        {
            using var db = GetInMemoryDb();
            var transitions = new OrderStatusTransitionService();
            var notificationService = new Mock<INotificationService>();
            var service = new OrderStatusService(db, transitions, notificationService.Object, new NullLogger<OrderStatusService>());

            await Assert.ThrowsAsync<OrderNotFoundException>(() =>
                service.ChangeStatusAsync(Guid.NewGuid(), OrderStatus.Preparing, null));
        }

        [Fact]
        public async Task ChangeStatusAsync_Cancellation_ShouldWork()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrder(db, OrderStatus.Preparing);
            var transitions = new OrderStatusTransitionService();
            var notificationService = new Mock<INotificationService>();
            var service = new OrderStatusService(db, transitions, notificationService.Object, new NullLogger<OrderStatusService>());

            var result = await service.ChangeStatusAsync(order.Id, OrderStatus.Cancelled, "Cliente solicitó cancelación.");
            Assert.Equal("Cancelled", result.Status);
        }

        [Fact]
        public async Task ChangeStatusAsync_ShippedOnlyForNationalShipping_ShouldRejectForDelivery()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrder(db, OrderStatus.Ready, DeliveryType.Delivery);
            var transitions = new OrderStatusTransitionService();
            var notificationService = new Mock<INotificationService>();
            var service = new OrderStatusService(db, transitions, notificationService.Object, new NullLogger<OrderStatusService>());

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                service.ChangeStatusAsync(order.Id, OrderStatus.Shipped, null));
            Assert.Contains("Transición inválida", ex.Message);
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldReturnOrderedHistory()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrder(db, OrderStatus.PaymentConfirmed);
            var transitions = new OrderStatusTransitionService();
            var notificationService = new Mock<INotificationService>();
            var service = new OrderStatusService(db, transitions, notificationService.Object, new NullLogger<OrderStatusService>());

            await service.ChangeStatusAsync(order.Id, OrderStatus.Preparing, null);
            await service.ChangeStatusAsync(order.Id, OrderStatus.Ready, null);

            var history = await service.GetHistoryAsync(order.Id);
            Assert.Equal(3, history.Count);
            Assert.Equal("PaymentConfirmed", history[0].Status);
            Assert.Equal("Preparing", history[1].Status);
            Assert.Equal("Ready", history[2].Status);
        }
    }
}
