using System;
using System.Threading.Tasks;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Dashboard;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraNova.UnitTests
{
    public class DashboardServiceTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly DashboardService _service;

        public DashboardServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _db = new AppDbContext(options);
            _service = new DashboardService(_db);
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        [Fact]
        public async Task GetSummaryAsync_EmptyDatabase_ReturnsZeros()
        {
            var result = await _service.GetSummaryAsync();

            Assert.Equal(0, result.Orders.WaitingQuote);
            Assert.Equal(0, result.Orders.Delivered);
            Assert.Equal(0, result.Quotes.Pending);
            Assert.Equal(0, result.Payments.Confirmed);
            Assert.Equal(0, result.Today.Orders);
            Assert.Equal(0m, result.Today.Sales);
        }

        [Fact]
        public async Task GetSummaryAsync_WithOrders_ReturnsCorrectCounts()
        {
            var order1 = new Order { OrderCode = "ORD-1", Status = OrderStatus.WaitingQuote, CreatedAt = DateTimeOffset.UtcNow };
            var order2 = new Order { OrderCode = "ORD-2", Status = OrderStatus.Preparing, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) };
            
            _db.Set<Order>().AddRange(order1, order2);
            await _db.SaveChangesAsync();

            var result = await _service.GetSummaryAsync();

            Assert.Equal(1, result.Orders.WaitingQuote);
            Assert.Equal(1, result.Orders.Preparing);
            Assert.Equal(0, result.Orders.Delivered);
            Assert.Equal(1, result.Today.Orders); // order1 is today, order2 is -2 days
        }

        [Fact]
        public async Task GetSummaryAsync_WithPayments_ReturnsCorrectSales()
        {
            var order1 = new Order { OrderCode = "ORD-1", Status = OrderStatus.Delivered };
            var payment1 = new Payment 
            { 
                OrderId = order1.Id, 
                Status = PaymentStatus.Confirmed, 
                Amount = 100m,
                VerifiedAt = DateTimeOffset.UtcNow
            };

            var payment2 = new Payment 
            { 
                OrderId = Guid.NewGuid(), 
                Status = PaymentStatus.Pending, 
                Amount = 50m,
                VerifiedAt = DateTimeOffset.UtcNow
            };

            var payment3 = new Payment 
            { 
                OrderId = Guid.NewGuid(), 
                Status = PaymentStatus.Confirmed, 
                Amount = 200m,
                VerifiedAt = DateTimeOffset.UtcNow.AddDays(-2) // not today
            };

            _db.Set<Order>().Add(order1);
            _db.Set<Payment>().AddRange(payment1, payment2, payment3);
            await _db.SaveChangesAsync();

            var result = await _service.GetSummaryAsync();

            Assert.Equal(1, result.Payments.PendingVerification);
            Assert.Equal(2, result.Payments.Confirmed);
            Assert.Equal(1, result.Today.ConfirmedPayments); // only payment1 is today
            Assert.Equal(100m, result.Today.Sales); // only payment1 is today
        }
    }
}
