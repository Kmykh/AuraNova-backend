using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.AdminOrders.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraNova.UnitTests
{
    public class AdminOrderQueryServiceTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly AdminOrderQueryService _service;

        public AdminOrderQueryServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _db = new AppDbContext(options);
            _service = new AdminOrderQueryService(_db);
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        [Fact]
        public async Task GetOrdersAsync_ReturnsPagedResults()
        {
            var customer = new Customer { Name = "Test", Phone = "123" };
            _db.Set<Customer>().Add(customer);

            for (int i = 0; i < 25; i++)
            {
                _db.Set<Order>().Add(new Order { OrderCode = $"ORD-{i}", Status = OrderStatus.Delivered, Customer = customer });
            }
            await _db.SaveChangesAsync();

            var request = new AdminOrderFilterRequest { Page = 1, PageSize = 10 };
            var response = await _service.GetOrdersAsync(request);

            Assert.Equal(25, response.TotalItems);
            Assert.Equal(3, response.TotalPages);
            Assert.Equal(10, response.Items.Count);
        }

        [Fact]
        public async Task GetOrdersAsync_WithStatusFilter_ReturnsFilteredResults()
        {
            var customer = new Customer { Name = "Test", Phone = "123" };
            _db.Set<Customer>().Add(customer);

            _db.Set<Order>().Add(new Order { OrderCode = "ORD-1", Status = OrderStatus.WaitingQuote, Customer = customer });
            _db.Set<Order>().Add(new Order { OrderCode = "ORD-2", Status = OrderStatus.Delivered, Customer = customer });
            await _db.SaveChangesAsync();

            var request = new AdminOrderFilterRequest { Status = OrderStatus.WaitingQuote };
            var response = await _service.GetOrdersAsync(request);

            Assert.Equal(1, response.TotalItems);
            Assert.Equal("ORD-1", response.Items.First().OrderCode);
        }

        [Fact]
        public async Task GetOrdersAsync_WithSearchFilter_ReturnsFilteredResults()
        {
            var customer = new Customer { Name = "Maria Silva", Phone = "999999999" };
            var order1 = new Order { OrderCode = "PED-2026", Customer = customer };
            var order2 = new Order { OrderCode = "ORD-XYZ" };
            
            _db.Set<Customer>().Add(customer);
            _db.Set<Order>().AddRange(order1, order2);
            await _db.SaveChangesAsync();

            var request = new AdminOrderFilterRequest { Search = "maria" };
            var response = await _service.GetOrdersAsync(request);

            Assert.Equal(1, response.TotalItems);
            Assert.Equal("PED-2026", response.Items.First().OrderCode);
        }

        [Fact]
        public async Task GetOrderDetailAsync_ExistingOrder_ReturnsDetails()
        {
            var customer = new Customer { Name = "Test", Phone = "123" };
            _db.Set<Customer>().Add(customer);

            var order = new Order 
            { 
                OrderCode = "DETAIL-1",
                Status = OrderStatus.Preparing,
                DeliveryType = DeliveryType.Delivery,
                Customer = customer
            };
            _db.Set<Order>().Add(order);
            await _db.SaveChangesAsync();

            var response = await _service.GetOrderDetailAsync(order.Id);

            Assert.NotNull(response);
            Assert.Equal("DETAIL-1", response.OrderCode);
            Assert.Equal("Preparing", response.Status);
        }

        [Fact]
        public async Task GetOrderDetailAsync_NonExistingOrder_ReturnsNull()
        {
            var response = await _service.GetOrderDetailAsync(Guid.NewGuid());
            Assert.Null(response);
        }
    }
}
