using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuraNova.IntegrationTests
{
    public class TrackingEndpointTests
    {
        private class FakeStorageService : IFileStorageService
        {
            public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder) =>
                Task.FromResult($"{folder}/fake.jpg");
            public Task DeleteAsync(string path) => Task.CompletedTask;
        }

        private WebApplicationFactory<Program> CreateFactory(string dbName)
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JwtSettings:SecretKey", "TestSecretKeyForIntegrationTestsMustBeLongEnough123456");
                builder.UseSetting("JwtSettings:Issuer", "AuraNova.Test");
                builder.UseSetting("JwtSettings:Audience", "AuraNova.Test");
                builder.UseSetting("JwtSettings:ExpirationMinutes", "60");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
                builder.UseSetting("InitialAdmin:Enabled", "false");
                builder.UseSetting("PaymentSettings:YapeEnabled", "true");
                builder.UseSetting("PaymentSettings:YapeHolderName", "Test");

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(dbName));

                    var storageDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IFileStorageService));
                    if (storageDesc != null) services.Remove(storageDesc);
                    services.AddSingleton<IFileStorageService, FakeStorageService>();
                });
            });
        }

        private async Task<Order> SeedOrderWithHistory(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var customer = new Customer { Name = "Test Client", Phone = "123456789" };
            db.Customers.Add(customer);

            var order = new Order
            {
                CustomerId = customer.Id,
                OrderCode = "PED-2026-T00001",
                Status = OrderStatus.Preparing,
                DeliveryType = DeliveryType.Delivery,
                Subtotal = 80m,
                DeliveryCost = 7m,
                Total = 87m
            };
            db.Orders.Add(order);

            db.OrderStatusHistory.AddRange(
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.WaitingPayment },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PaymentConfirmed },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Preparing }
            );

            await db.SaveChangesAsync();
            return order;
        }

        [Fact]
        public async Task GET_Tracking_ValidTokenAndCode_ShouldReturn200()
        {
            await using var factory = CreateFactory("it_tracking_ok_" + Guid.NewGuid());
            var client = factory.CreateClient();
            var order = await SeedOrderWithHistory(factory.Services);

            var response = await client.GetAsync($"/api/public/orders/{order.OrderCode}/tracking/{order.TrackingToken}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("PED-2026-T00001", json.GetProperty("orderCode").GetString());
            Assert.Equal("Preparing", json.GetProperty("status").GetString());
            Assert.Equal(3, json.GetProperty("timeline").GetArrayLength());

            // Should NOT contain sensitive data
            var raw = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("123456789", raw); // No phone
            Assert.DoesNotContain(order.TrackingToken, raw); // No token in response body
        }

        [Fact]
        public async Task GET_Tracking_InvalidToken_ShouldReturn404()
        {
            await using var factory = CreateFactory("it_tracking_bad_" + Guid.NewGuid());
            var client = factory.CreateClient();
            var order = await SeedOrderWithHistory(factory.Services);

            var response = await client.GetAsync($"/api/public/orders/{order.OrderCode}/tracking/bad-token");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GET_Tracking_InvalidOrderCode_ShouldReturn404()
        {
            await using var factory = CreateFactory("it_tracking_nocode_" + Guid.NewGuid());
            var client = factory.CreateClient();

            var response = await client.GetAsync("/api/public/orders/PED-FAKE-999/tracking/any-token");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
