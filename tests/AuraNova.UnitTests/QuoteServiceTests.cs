using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Quotes.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using AuraNova.Infrastructure.Quotes;
using AuraNova.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class QuoteServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private async Task<(Order order, Quote quote)> SeedOrderWithQuote(AppDbContext db)
        {
            var customer = new Customer { Name = "María", Phone = "999999999" };
            db.Customers.Add(customer);

            var product = new Product { Name = "Caja Floral", Price = 80.00m, Stock = 10, IsAvailable = true };
            db.Products.Add(product);

            var order = new Order
            {
                CustomerId = customer.Id,
                Customer = customer,
                OrderCode = "PED-2026-000001",
                DeliveryType = DeliveryType.NationalShipping,
                Subtotal = 80.00m,
                DeliveryCost = null,
                Total = null,
                Status = OrderStatus.WaitingQuote,
                Department = "Lima",
                Province = "Lima",
                District = "Miraflores"
            };
            db.Orders.Add(order);

            var quote = new Quote { OrderId = order.Id };
            db.Quotes.Add(quote);

            await db.SaveChangesAsync();
            return (order, quote);
        }

        [Fact]
        public async Task UpdateAsync_ShouldQuoteSuccessfully()
        {
            using var db = GetInMemoryDb();
            var (order, quote) = await SeedOrderWithQuote(db);
            var notificationService = new Mock<INotificationService>();
            var service = new QuoteService(db, notificationService.Object, new NullLogger<QuoteService>());

            var result = await service.UpdateAsync(quote.Id, new UpdateQuoteRequest
            {
                ShippingCost = 20.00m,
                Notes = "Envío por Cruz del Sur"
            });

            Assert.Equal("Ready", result.Status);
            Assert.Equal(20.00m, result.ShippingCost);
            Assert.Equal(100.00m, result.Total);
        }

        [Fact]
        public async Task UpdateAsync_ShouldRejectNegativeShippingCost()
        {
            using var db = GetInMemoryDb();
            var (_, quote) = await SeedOrderWithQuote(db);
            var notificationService = new Mock<INotificationService>();
            var service = new QuoteService(db, notificationService.Object, new NullLogger<QuoteService>());

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                service.UpdateAsync(quote.Id, new UpdateQuoteRequest { ShippingCost = -5.00m }));
            Assert.Contains("negativo", ex.Message);
        }

        [Fact]
        public async Task UpdateAsync_QuoteShouldBecomeReady()
        {
            using var db = GetInMemoryDb();
            var (_, quote) = await SeedOrderWithQuote(db);
            var notificationService = new Mock<INotificationService>();
            var service = new QuoteService(db, notificationService.Object, new NullLogger<QuoteService>());

            await service.UpdateAsync(quote.Id, new UpdateQuoteRequest { ShippingCost = 15.00m });

            var dbQuote = await db.Quotes.FindAsync(quote.Id);
            Assert.Equal(QuoteStatus.Ready, dbQuote!.Status);
            Assert.NotNull(dbQuote.QuotedAt);
        }

        [Fact]
        public async Task UpdateAsync_OrderShouldBecomeQuoteReady()
        {
            using var db = GetInMemoryDb();
            var (order, quote) = await SeedOrderWithQuote(db);
            var notificationService = new Mock<INotificationService>();
            var service = new QuoteService(db, notificationService.Object, new NullLogger<QuoteService>());

            await service.UpdateAsync(quote.Id, new UpdateQuoteRequest { ShippingCost = 20.00m });

            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.Equal(OrderStatus.QuoteReady, dbOrder!.Status);
            Assert.Equal(20.00m, dbOrder.DeliveryCost);
            Assert.Equal(100.00m, dbOrder.Total);
        }


        [Fact]
        public async Task UpdateAsync_ShouldRejectIfNotWaitingQuote()
        {
            using var db = GetInMemoryDb();
            var (order, quote) = await SeedOrderWithQuote(db);

            // Force order to different status
            order.Status = OrderStatus.Cancelled;
            await db.SaveChangesAsync();

            var notificationService = new Mock<INotificationService>();
            var service = new QuoteService(db, notificationService.Object, new NullLogger<QuoteService>());

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                service.UpdateAsync(quote.Id, new UpdateQuoteRequest { ShippingCost = 20.00m }));
            Assert.Contains("WaitingQuote", ex.Message);
        }
    }
}
