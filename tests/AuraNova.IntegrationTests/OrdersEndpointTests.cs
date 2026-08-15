using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraNova.IntegrationTests
{
    public class OrdersEndpointTests
    {
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

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(dbName));
                });
            });
        }

        private async Task<Product> SeedProduct(IServiceProvider services, decimal price = 79.90m)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = new Product
            {
                Name = "Caja Floral Romántica",
                Price = price,
                Stock = 10,
                IsAvailable = true
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return product;
        }

        private async Task<DeliveryZone> SeedDeliveryZone(IServiceProvider services, decimal cost = 7.00m)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var zone = new DeliveryZone
            {
                Name = "El Tambo",
                District = "El Tambo",
                Cost = cost,
                IsActive = true
            };
            db.DeliveryZones.Add(zone);
            await db.SaveChangesAsync();
            return zone;
        }

        private async Task<MeetingPoint> SeedMeetingPoint(IServiceProvider services, decimal cost = 0m)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var point = new MeetingPoint
            {
                Name = "Parque Túpac Amaru",
                Address = "Parque Túpac Amaru, Huancayo",
                Cost = cost,
                IsActive = true
            };
            db.MeetingPoints.Add(point);
            await db.SaveChangesAsync();
            return point;
        }

        // =====================================================================
        // DELIVERY
        // =====================================================================

        [Fact]
        public async Task POST_Orders_Delivery_ShouldReturn201()
        {
            await using var factory = CreateFactory("it_delivery_" + Guid.NewGuid());
            var client = factory.CreateClient();
            var product = await SeedProduct(factory.Services, price: 80.00m);
            var zone = await SeedDeliveryZone(factory.Services, cost: 7.00m);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [new() { ProductId = product.Id, Quantity = 1 }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "Delivery",
                    DeliveryZoneId = zone.Id,
                    DeliveryAddress = "Jr. Lima 123"
                }
            };

            var response = await client.PostAsJsonAsync("/api/orders", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Delivery", json.GetProperty("deliveryType").GetString());
            Assert.Equal(80.00m, json.GetProperty("subtotal").GetDecimal());
            Assert.Equal(7.00m, json.GetProperty("deliveryCost").GetDecimal());
            Assert.Equal(87.00m, json.GetProperty("total").GetDecimal());
            Assert.Equal("WaitingPayment", json.GetProperty("status").GetString());
        }

        // =====================================================================
        // MEETING POINT
        // =====================================================================

        [Fact]
        public async Task POST_Orders_MeetingPoint_ShouldReturn201()
        {
            await using var factory = CreateFactory("it_mp_" + Guid.NewGuid());
            var client = factory.CreateClient();
            var product = await SeedProduct(factory.Services, price: 80.00m);
            var point = await SeedMeetingPoint(factory.Services, cost: 0m);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "Carlos", Phone = "888888888" },
                Items = [new() { ProductId = product.Id, Quantity = 1 }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "MeetingPoint",
                    MeetingPointId = point.Id
                }
            };

            var response = await client.PostAsJsonAsync("/api/orders", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("MeetingPoint", json.GetProperty("deliveryType").GetString());
            Assert.Equal(80.00m, json.GetProperty("subtotal").GetDecimal());
            Assert.Equal(0m, json.GetProperty("deliveryCost").GetDecimal());
            Assert.Equal(80.00m, json.GetProperty("total").GetDecimal());
            Assert.Equal("WaitingPayment", json.GetProperty("status").GetString());
        }

        // =====================================================================
        // NATIONAL SHIPPING
        // =====================================================================

        [Fact]
        public async Task POST_Orders_NationalShipping_ShouldReturn201WithNullCosts()
        {
            await using var factory = CreateFactory("it_ns_" + Guid.NewGuid());
            var client = factory.CreateClient();
            var product = await SeedProduct(factory.Services);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "Ana", Phone = "777777777" },
                Items = [new() { ProductId = product.Id, Quantity = 1 }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "NationalShipping",
                    Department = "Lima",
                    Province = "Lima",
                    District = "Miraflores",
                    DeliveryAddress = "Av. Larco 400"
                }
            };

            var response = await client.PostAsJsonAsync("/api/orders", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("NationalShipping", json.GetProperty("deliveryType").GetString());
            Assert.Equal("WaitingQuote", json.GetProperty("status").GetString());

            // DeliveryCost and Total should be null
            Assert.Equal(JsonValueKind.Null, json.GetProperty("deliveryCost").ValueKind);
            Assert.Equal(JsonValueKind.Null, json.GetProperty("total").ValueKind);

            // Verify Quote was created in DB
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var quote = await db.Quotes.FirstOrDefaultAsync();
            Assert.NotNull(quote);
            Assert.Equal(AuraNova.Domain.Enums.QuoteStatus.Pending, quote.Status);
        }

        // =====================================================================
        // VALIDATION
        // =====================================================================

        [Fact]
        public async Task POST_Orders_ShouldReturn404_WhenProductNotFound()
        {
            await using var factory = CreateFactory("it_404_" + Guid.NewGuid());
            var client = factory.CreateClient();
            var zone = await SeedDeliveryZone(factory.Services);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1 }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "Delivery",
                    DeliveryZoneId = zone.Id,
                    DeliveryAddress = "test"
                }
            };

            var response = await client.PostAsJsonAsync("/api/orders", request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task POST_Orders_ShouldReturn400_WhenEmptyItems()
        {
            await using var factory = CreateFactory("it_empty_" + Guid.NewGuid());
            var client = factory.CreateClient();

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery" }
            };

            var response = await client.PostAsJsonAsync("/api/orders", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
