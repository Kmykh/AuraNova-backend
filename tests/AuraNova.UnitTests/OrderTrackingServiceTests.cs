using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraNova.UnitTests
{
    public class OrderTrackingServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private async Task<Order> SeedOrderWithHistory(AppDbContext db)
        {
            var customer = new Customer { Name = "María", Phone = "999999999", Email = "maria@test.com" };
            db.Customers.Add(customer);

            var order = new Order
            {
                CustomerId = customer.Id,
                OrderCode = "PED-2026-000001",
                Status = OrderStatus.Preparing,
                DeliveryType = DeliveryType.Delivery,
                Subtotal = 80m,
                DeliveryCost = 7m,
                Total = 87m
            };
            db.Orders.Add(order);

            // Seed history
            db.OrderStatusHistory.AddRange(
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.WaitingPayment },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PaymentReported },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PaymentConfirmed },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Preparing }
            );

            await db.SaveChangesAsync();
            return order;
        }

        [Fact]
        public async Task GetTrackingAsync_CorrectToken_ShouldReturnTracking()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db);

            var result = await service.GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.NotNull(result);
            Assert.Equal("PED-2026-000001", result!.OrderCode);
            Assert.Equal("Preparing", result.Status);
            Assert.Equal("Preparando tu pedido", result.StatusLabel);
            Assert.Equal("Delivery", result.DeliveryType);
            Assert.Equal(87m, result.Total);
        }

        [Fact]
        public async Task GetTrackingAsync_CorrectToken_ShouldReturnTimeline()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db);

            var result = await service.GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.NotNull(result);
            Assert.Equal(4, result!.Timeline.Count);
            Assert.Equal("WaitingPayment", result.Timeline[0].Status);
            Assert.Equal("Preparing", result.Timeline[3].Status);
            Assert.True(result.Timeline[0].Completed);
        }

        [Fact]
        public async Task GetTrackingAsync_WrongToken_ShouldReturnNull()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db);

            var result = await service.GetTrackingAsync(order.OrderCode, "wrong-token");
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTrackingAsync_WrongOrderCode_ShouldReturnNull()
        {
            using var db = GetInMemoryDb();
            await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db);

            var result = await service.GetTrackingAsync("PED-FAKE-999999", "any-token");
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTrackingAsync_ShouldNotExposePhone()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db);

            var result = await service.GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.NotNull(result);
            // PublicTrackingResponse has no phone/email/token properties
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.DoesNotContain("999999999", json);
            Assert.DoesNotContain("maria@test.com", json);
            Assert.DoesNotContain(order.TrackingToken, json);
        }
    }
}
