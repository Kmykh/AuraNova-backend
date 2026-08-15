using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AuraNova.Application.Payments.DTOs;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraNova.IntegrationTests
{
    public class PaymentsEndpointTests
    {
        private class FakeStorageService : IFileStorageService
        {
            public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder)
            {
                return Task.FromResult($"{folder}/fake-uuid.jpg");
            }

            public Task DeleteAsync(string path)
            {
                return Task.CompletedTask;
            }
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
                builder.UseSetting("PaymentSettings:YapeHolderName", "Test Holder");

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(dbName));

                    // Override storage service with fake
                    var storageDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IFileStorageService));
                    if (storageDescriptor != null)
                        services.Remove(storageDescriptor);
                    services.AddSingleton<IFileStorageService, FakeStorageService>();
                });
            });
        }

        private async Task<(Order order, Payment payment)> SeedWaitingPaymentOrder(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var customer = new Customer { Name = "Test", Phone = "123" };
            db.Customers.Add(customer);

            var order = new Order
            {
                CustomerId = customer.Id,
                Customer = customer,
                OrderCode = "PED-2026-999999",
                Status = OrderStatus.WaitingPayment,
                Total = 150m,
                Subtotal = 150m
            };
            db.Orders.Add(order);

            var payment = new Payment
            {
                OrderId = order.Id,
                Status = PaymentStatus.Pending,
                Amount = 150m,
                Method = PaymentMethod.Yape
            };
            db.Payments.Add(payment);

            await db.SaveChangesAsync();
            return (order, payment);
        }

        [Fact]
        public async Task GET_PaymentInfo_ShouldReturn200()
        {
            await using var factory = CreateFactory("it_payments_info_" + Guid.NewGuid());

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.BusinessSettings.Add(new AuraNova.Domain.Entities.BusinessSettings 
                { 
                    YapeHolderName = "Test Holder", 
                    YapeQrImageUrl = "test.png", 
                    BusinessName = "Test Business" 
                });
                await db.SaveChangesAsync();
            }

            var client = factory.CreateClient();

            var response = await client.GetAsync("/api/payment-info");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("enabled").GetBoolean());
            Assert.Equal("Yape", json.GetProperty("method").GetString());
            Assert.Equal("Test Holder", json.GetProperty("holderName").GetString());
        }

        [Fact]
        public async Task POST_PaymentEvidence_ShouldReturn200()
        {
            await using var factory = CreateFactory("it_payments_evid_" + Guid.NewGuid());
            var client = factory.CreateClient();
            var (order, _) = await SeedWaitingPaymentOrder(factory.Services);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "evidence", "test.jpg");

            var response = await client.PostAsync($"/api/orders/{order.Id}/payment-evidence", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Reported", json.GetProperty("status").GetString());
            Assert.Contains("fake-uuid", json.GetProperty("evidenceUrl").GetString());
        }
    }
}
