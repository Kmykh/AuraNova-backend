using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;


namespace AuraNova.UnitTests
{
    public class OrderServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private ILogger<OrderService> GetLogger() => new NullLogger<OrderService>();

        private async Task<Product> SeedProduct(AppDbContext db, string name = "Caja Floral Romántica",
            decimal price = 79.90m, int stock = 10, bool isAvailable = true)
        {
            var product = new Product
            {
                Name = name,
                Price = price,
                Stock = stock,
                IsAvailable = isAvailable
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return product;
        }

        private async Task<DeliveryZone> SeedDeliveryZone(AppDbContext db, string name = "El Tambo",
            decimal cost = 7.00m, bool isActive = true)
        {
            var zone = new DeliveryZone
            {
                Name = name,
                District = name,
                Cost = cost,
                IsActive = isActive
            };
            db.DeliveryZones.Add(zone);
            await db.SaveChangesAsync();
            return zone;
        }

        private async Task<MeetingPoint> SeedMeetingPoint(AppDbContext db, string name = "Parque Túpac Amaru",
            decimal cost = 0m, bool isActive = true)
        {
            var point = new MeetingPoint
            {
                Name = name,
                Address = $"{name}, Huancayo",
                Cost = cost,
                IsActive = isActive
            };
            db.MeetingPoints.Add(point);
            await db.SaveChangesAsync();
            return point;
        }

        private CreateOrderRequest BuildDeliveryRequest(Guid productId, Guid zoneId, int quantity = 2)
        {
            return new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest
                {
                    Name = "María",
                    Phone = "999999999",
                    Email = "maria@email.com"
                },
                Items = [new() { ProductId = productId, Quantity = quantity }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "Delivery",
                    DeliveryZoneId = zoneId,
                    DeliveryAddress = "Jr. Lima 123, referencia: frente al parque"
                }
            };
        }

        private CreateOrderRequest BuildMeetingPointRequest(Guid productId, Guid meetingPointId, int quantity = 2)
        {
            return new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest
                {
                    Name = "María",
                    Phone = "999999999"
                },
                Items = [new() { ProductId = productId, Quantity = quantity }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "MeetingPoint",
                    MeetingPointId = meetingPointId
                }
            };
        }

        private CreateOrderRequest BuildNationalShippingRequest(Guid productId, int quantity = 2)
        {
            return new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest
                {
                    Name = "María",
                    Phone = "999999999"
                },
                Items = [new() { ProductId = productId, Quantity = quantity }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "NationalShipping",
                    Department = "Lima",
                    Province = "Lima",
                    District = "Miraflores",
                    DeliveryAddress = "Av. Larco 400"
                }
            };
        }

        // =====================================================================
        // DELIVERY TESTS
        // =====================================================================

        [Fact]
        public async Task CreateAsync_Delivery_ShouldCreateOrderSuccessfully()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var zone = await SeedDeliveryZone(db, cost: 7.00m);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildDeliveryRequest(product.Id, zone.Id));

            Assert.NotNull(result);
            Assert.Equal("Delivery", result.DeliveryType);
            Assert.Equal("WaitingPayment", result.Status);
            Assert.StartsWith("PED-", result.OrderCode);
        }

        [Fact]
        public async Task CreateAsync_Delivery_ShouldRejectNonExistentZone()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildDeliveryRequest(product.Id, Guid.NewGuid());

            await Assert.ThrowsAsync<OrderNotFoundException>(() => service.CreateAsync(request));
        }

        [Fact]
        public async Task CreateAsync_Delivery_ShouldRejectInactiveZone()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var zone = await SeedDeliveryZone(db, isActive: false);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildDeliveryRequest(product.Id, zone.Id);

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("no está activa", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_Delivery_ShouldRejectWithoutAddress()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildDeliveryRequest(product.Id, zone.Id);
            request.Delivery.DeliveryAddress = null;

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("DeliveryAddress", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_Delivery_ShouldGetCostFromZone()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, price: 80.00m);
            var zone = await SeedDeliveryZone(db, cost: 7.00m);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildDeliveryRequest(product.Id, zone.Id, quantity: 1));

            Assert.Equal(7.00m, result.DeliveryCost);
        }

        [Fact]
        public async Task CreateAsync_Delivery_ShouldCalculateTotalCorrectly()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, price: 80.00m);
            var zone = await SeedDeliveryZone(db, cost: 7.00m);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildDeliveryRequest(product.Id, zone.Id, quantity: 1));

            Assert.Equal(80.00m, result.Subtotal);
            Assert.Equal(7.00m, result.DeliveryCost);
            Assert.Equal(87.00m, result.Total);
        }

        // =====================================================================
        // MEETING POINT TESTS
        // =====================================================================

        [Fact]
        public async Task CreateAsync_MeetingPoint_ShouldCreateOrderSuccessfully()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var point = await SeedMeetingPoint(db, cost: 0m);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildMeetingPointRequest(product.Id, point.Id));

            Assert.Equal("MeetingPoint", result.DeliveryType);
            Assert.Equal("WaitingPayment", result.Status);
        }

        [Fact]
        public async Task CreateAsync_MeetingPoint_ShouldRejectNonExistentPoint()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildMeetingPointRequest(product.Id, Guid.NewGuid());

            await Assert.ThrowsAsync<OrderNotFoundException>(() => service.CreateAsync(request));
        }

        [Fact]
        public async Task CreateAsync_MeetingPoint_ShouldRejectInactivePoint()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var point = await SeedMeetingPoint(db, isActive: false);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildMeetingPointRequest(product.Id, point.Id);

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("no está activo", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_MeetingPoint_ShouldGetCostFromPoint()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, price: 80.00m);
            var point = await SeedMeetingPoint(db, cost: 5.00m);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildMeetingPointRequest(product.Id, point.Id, quantity: 1));

            Assert.Equal(5.00m, result.DeliveryCost);
            Assert.Equal(85.00m, result.Total);
        }

        // =====================================================================
        // NATIONAL SHIPPING TESTS
        // =====================================================================

        [Fact]
        public async Task CreateAsync_NationalShipping_ShouldCreateQuote()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            var quote = await db.Quotes.FirstOrDefaultAsync();
            Assert.NotNull(quote);
            Assert.Equal(QuoteStatus.Pending, quote.Status);
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_ShouldSetStatusWaitingQuote()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            Assert.Equal("WaitingQuote", result.Status);
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_DeliveryCostShouldBeNull()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            Assert.Null(result.DeliveryCost);
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_TotalShouldBeNull()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var result = await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            Assert.Null(result.Total);
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_ShouldRequireDestination()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildNationalShippingRequest(product.Id);
            request.Delivery.Department = null;

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("Department", ex.Message);
        }

        // =====================================================================
        // GENERAL VALIDATION TESTS (updated from Phase 5)
        // =====================================================================

        [Fact]
        public async Task CreateAsync_ShouldRejectEmptyItems()
        {
            using var db = GetInMemoryDb();
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("al menos un producto", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_ShouldRejectDuplicateProducts()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [
                    new() { ProductId = product.Id, Quantity = 2 },
                    new() { ProductId = product.Id, Quantity = 3 }
                ],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("duplicados", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_ShouldRejectNonExistentProduct()
        {
            using var db = GetInMemoryDb();
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildDeliveryRequest(Guid.NewGuid(), zone.Id);

            await Assert.ThrowsAsync<OrderNotFoundException>(() => service.CreateAsync(request));
        }

        [Fact]
        public async Task CreateAsync_ShouldRejectUnavailableProduct()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, isAvailable: false);
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildDeliveryRequest(product.Id, zone.Id);

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("no está disponible", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_ShouldRejectInsufficientStock()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, stock: 3);
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = BuildDeliveryRequest(product.Id, zone.Id, quantity: 5);

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("Stock insuficiente", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_ShouldRejectInvalidDeliveryType()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [new() { ProductId = product.Id, Quantity = 1 }],
                Delivery = new CreateOrderDeliveryRequest { Type = "InvalidType" }
            };

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("inválido", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_ShouldStoreHistoricalDeliveryCost()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, price: 50.00m);
            var zone = await SeedDeliveryZone(db, cost: 7.00m);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            await service.CreateAsync(BuildDeliveryRequest(product.Id, zone.Id, quantity: 1));

            // Now change zone cost — old order should keep original cost
            zone.Cost = 15.00m;
            await db.SaveChangesAsync();

            var order = await db.Orders.FirstAsync();
            Assert.Equal(7.00m, order.DeliveryCost); // Historical snapshot preserved
        }

        [Fact]
        public async Task CreateAsync_ShouldNotDeductStock()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, stock: 10);
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var service = new OrderService(db, notificationService.Object, GetLogger());

            await service.CreateAsync(BuildDeliveryRequest(product.Id, zone.Id, quantity: 3));

            var dbProduct = await db.Products.FindAsync(product.Id);
            Assert.Equal(10, dbProduct!.Stock); // Stock unchanged
        }
    }
}
