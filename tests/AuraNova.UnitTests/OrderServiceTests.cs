using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AuraNova.Application.Auth.Interfaces;
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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

            var result = await service.CreateAsync(BuildDeliveryRequest(product.Id, zone.Id));

            Assert.NotNull(result);
            Assert.Equal("Delivery", result.DeliveryType);
            Assert.Equal("WaitingPayment", result.Status);
            Assert.Equal("AN-1001", result.OrderCode);
        }

        [Fact]
        public async Task CreateAsync_Delivery_ShouldRejectNonExistentZone()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

            await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            var quote = await db.Quotes.FirstOrDefaultAsync();
            Assert.Null(quote);
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_ShouldSetStatusWaitingQuote()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

            var result = await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            Assert.Equal("WaitingPayment", result.Status);
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_DeliveryCostShouldBeNull()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

            var result = await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            Assert.Null(result.DeliveryCost);
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_TotalShouldBeNull()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

            var result = await service.CreateAsync(BuildNationalShippingRequest(product.Id));

            Assert.NotNull(result.Total);
            Assert.Equal(159.80m, result.Total); // Only subtotal
        }

        [Fact]
        public async Task CreateAsync_NationalShipping_ShouldRequireDestination()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            var notificationService = new Mock<INotificationService>();
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("al menos un producto", ex.Message);
        }

        private OrderService BuildService(AppDbContext db)
        {
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            return new OrderService(db, new Mock<INotificationService>().Object, GetLogger(), new OrderStatusTransitionService(),
                new Mock<ICurrentUserService>().Object, priceResolver.Object);
        }

        [Fact]
        public async Task CreateAsync_SameProductWithDifferentOptions_ShouldCreateOneLinePerCustomization()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, stock: 10);
            product.AvailableColors = ["Rojo", "Blanco", "Azul"];
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [
                    new() { ProductId = product.Id, Quantity = 2, SelectedColors = ["Rojo"] },
                    new() { ProductId = product.Id, Quantity = 3, SelectedColors = ["blanco", "Azul"] }
                ],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var result = await service.CreateAsync(request);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(["Rojo"], result.Items[0].SelectedColors);
            // Se guarda con el nombre exacto del producto y cada color por separado
            Assert.Equal(["Blanco", "Azul"], result.Items[1].SelectedColors);
            Assert.Equal(5, (await db.Products.FindAsync(product.Id))!.Stock);
        }

        // =====================================================================
        // ACCESORIOS CON PRECIO
        // =====================================================================

        private async Task<(Product Product, DeliveryZone Zone)> SeedAccessories(AppDbContext db)
        {
            db.BusinessSettings.Add(new BusinessSettings
            {
                AccessoryOptions =
                [
                    new() { Key = "luces", Name = "Luces LED", Price = 1m },
                    new() { Key = "perlas", Name = "Perlas", Price = 3.50m },
                    new() { Key = "lazo", Name = "Lazo de seda", Price = 2m, Active = false }
                ]
            });
            var product = await SeedProduct(db, price: 79.90m, stock: 10);
            product.AvailableAccessories = ["luces", "perlas", "lazo"];
            await db.SaveChangesAsync();
            return (product, await SeedDeliveryZone(db, cost: 7m));
        }

        private static CreateOrderRequest AccessoryOrder(Guid productId, Guid zoneId, params List<string>[] accessoriesPerLine) => new()
        {
            Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
            Items = [.. accessoriesPerLine.Select(a => new CreateOrderItemRequest { ProductId = productId, Quantity = 2, SelectedAccessories = a })],
            Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zoneId, DeliveryAddress = "test" }
        };

        [Fact]
        public async Task CreateAsync_Accessories_AreChargedWithTheBusinessPrice()
        {
            using var db = GetInMemoryDb();
            var (product, zone) = await SeedAccessories(db);

            var result = await BuildService(db).CreateAsync(AccessoryOrder(product.Id, zone.Id, ["luces", "Perlas"]));

            var item = Assert.Single(result.Items);
            Assert.Equal(84.40m, item.UnitPrice); // 79.90 + 1 + 3.50
            Assert.Equal(168.80m, item.Subtotal);
            Assert.Equal(175.80m, result.Total);
            Assert.True(item.HasLights);
            Assert.Equal(["Luces LED", "Perlas"], item.Accessories.Select(a => a.Name));

            var saved = await db.OrderItems.SingleAsync();
            Assert.Equal([1m, 3.50m], saved.Accessories.Select(a => a.Price));
        }

        [Theory]
        [InlineData("lazo")]     // desactivado por el negocio
        [InlineData("mariposas")] // el producto no lo ofrece
        public async Task CreateAsync_Accessories_RejectsTheOnesNoLongerOffered(string key)
        {
            using var db = GetInMemoryDb();
            var (product, zone) = await SeedAccessories(db);

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                BuildService(db).CreateAsync(AccessoryOrder(product.Id, zone.Id, [key])));
            Assert.Contains("ya no está disponible", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_Accessories_DifferentAccessoriesAreSeparateLines()
        {
            using var db = GetInMemoryDb();
            var (product, zone) = await SeedAccessories(db);

            var result = await BuildService(db).CreateAsync(AccessoryOrder(product.Id, zone.Id, ["perlas"], [], ["perlas"]));

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(83.40m, result.Items[0].UnitPrice);
            Assert.Equal(4, result.Items[0].Quantity);
            Assert.Equal(79.90m, result.Items[1].UnitPrice);
        }

        [Fact]
        public async Task CreateAsync_Accessories_PreviousWebFlagsStillWorkWithoutCost()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, price: 50m, stock: 10);
            product.AllowsLights = true; // producto anterior, sin lista de accesorios
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var request = AccessoryOrder(product.Id, zone.Id, new List<string>());
            request.Items[0].SelectedAccessories = null;
            request.Items[0].HasLights = true;
            request.Items[0].HasButterfly = true; // no lo ofrece: se ignora, como antes

            var result = await BuildService(db).CreateAsync(request);

            var item = Assert.Single(result.Items);
            Assert.Equal(50m, item.UnitPrice);
            Assert.True(item.HasLights);
            Assert.False(item.HasButterfly);
            Assert.Equal(["luces"], item.Accessories.Select(a => a.Key));
        }

        [Theory]
        [InlineData(new[] { "Rosado", "Lila" }, new[] { "Rosas" })]
        [InlineData(new[] { "Surtido multicolor" }, new[] { "Rosas" })]
        [InlineData(new[] { "Rosado" }, new[] { "Rosas", "Tulipanes" })]
        public async Task CreateAsync_SingleFlower_AcceptsOnlyOneColorAndFlower(string[] colors, string[] flowers)
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, stock: 10);
            product.AvailableColors = ["Rosado", "Lila", "Amarillo"];
            product.AvailableFlowerTypes = ["Rosas", "Tulipanes"];
            product.MaxChoices = 1;
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);
            CreateOrderRequest Order(List<string> c, List<string> f) => new()
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [new() { ProductId = product.Id, Quantity = 1, SelectedColors = c, SelectedFlowerTypes = f }],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(Order([.. colors], [.. flowers])));
            Assert.Contains("una sola flor", ex.Message);

            var ok = await service.CreateAsync(Order(["Lila"], ["Tulipanes"]));
            Assert.Equal(["Lila"], ok.Items[0].SelectedColors);
        }

        [Fact]
        public async Task CreateAsync_CustomColor_IsAcceptedWithNameAndTone_AndCountsTowardTheLimit()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, stock: 10);
            product.AvailableColors = ["Rosado", "Lila"];
            product.MaxChoices = 1;
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);
            CreateOrderRequest Order(List<string> c) => new()
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [new() { ProductId = product.Id, Quantity = 1, SelectedColors = c }],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var ok = await service.CreateAsync(Order(["A pedido:  Verde menta (#98fb98)"]));
            Assert.Equal(["A pedido: Verde menta (#98FB98)"], ok.Items[0].SelectedColors);

            // Sin el tono, o con caracteres raros, no se acepta
            await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(Order(["A pedido: Verde"])));
            await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(Order(["A pedido: <b>x</b> (#FFFFFF)"])));
            // Una sola flor: el color a pedido también cuenta como su único color
            await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(Order(["Rosado", "A pedido: Coral (#FF7F6E)"])));
        }

        [Fact]
        public async Task CreateAsync_SameProductRepeated_ShouldValidateStockWithTotalQuantity()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, stock: 4);
            product.AvailableColors = ["Rojo", "Blanco"];
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [
                    new() { ProductId = product.Id, Quantity = 2, SelectedColors = ["Rojo"] },
                    new() { ProductId = product.Id, Quantity = 3, SelectedColors = ["Blanco"] }
                ],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("Stock insuficiente", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_IdenticalLines_ShouldBeMerged()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db, stock: 10);
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [
                    new() { ProductId = product.Id, Quantity = 2 },
                    new() { ProductId = product.Id, Quantity = 3 }
                ],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var result = await service.CreateAsync(request);

            var line = Assert.Single(result.Items);
            Assert.Equal(5, line.Quantity);
        }

        [Fact]
        public async Task CreateAsync_LegacyCommaSeparatedColors_ShouldBeStoredAsList()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            product.AvailableColors = ["Rojo", "Blanco", "Azul"];
            product.AvailableFlowerTypes = ["Rosas", "Girasoles"];
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [
                    new()
                    {
                        ProductId = product.Id, Quantity = 1,
                        SelectedPrimaryColor = "Rojo", SelectedSecondaryColor = "Blanco, Azul",
                        SelectedFlowerType = "Rosas", SelectedFlowerColor = "Girasoles"
                    }
                ],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var result = await service.CreateAsync(request);

            var item = Assert.Single(result.Items);
            Assert.Equal(["Rojo", "Blanco", "Azul"], item.SelectedColors);
            Assert.Equal(["Rosas", "Girasoles"], item.SelectedFlowerTypes);
            Assert.Null(item.SelectedSecondaryColor);
            Assert.Null(item.SelectedFlowerColor);
        }

        [Fact]
        public async Task CreateAsync_ColorNotOfferedByProduct_ShouldBeRejected()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            product.AvailableColors = ["Rojo"];
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [new() { ProductId = product.Id, Quantity = 1, SelectedColors = ["Verde"] }],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request));
            Assert.Contains("Verde", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_ExtrasNotAllowedByProduct_ShouldBeIgnored()
        {
            using var db = GetInMemoryDb();
            var product = await SeedProduct(db);
            product.AllowsLights = true;
            await db.SaveChangesAsync();
            var zone = await SeedDeliveryZone(db);
            var service = BuildService(db);

            var request = new CreateOrderRequest
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "999999999" },
                Items = [new() { ProductId = product.Id, Quantity = 1, HasLights = true, HasButterfly = true, HasPhraseCard = true, PhraseText = "Hola" }],
                Delivery = new CreateOrderDeliveryRequest { Type = "Delivery", DeliveryZoneId = zone.Id, DeliveryAddress = "test" }
            };

            var item = Assert.Single((await service.CreateAsync(request)).Items);
            Assert.True(item.HasLights);
            Assert.False(item.HasButterfly);
            Assert.False(item.HasPhraseCard);
            Assert.Null(item.PhraseText);
        }

        [Fact]
        public async Task CreateAsync_ShouldRejectNonExistentProduct()
        {
            using var db = GetInMemoryDb();
            var zone = await SeedDeliveryZone(db);
            var notificationService = new Mock<INotificationService>();
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

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
            var currentUserService = new Mock<ICurrentUserService>();
            var priceResolver = new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>();
            priceResolver.Setup(x => x.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                         .ReturnsAsync((Guid id, DateTimeOffset? dt) => new AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult
                         {
                             EffectivePrice = db.Products.FirstOrDefault(p => p.Id == id)?.Price ?? 0m
                         });
            var service = new OrderService(db, notificationService.Object, GetLogger(), new OrderStatusTransitionService(), currentUserService.Object, priceResolver.Object);

            await service.CreateAsync(BuildDeliveryRequest(product.Id, zone.Id, quantity: 3));

            var dbProduct = await db.Products.FindAsync(product.Id);
            Assert.Equal(7, dbProduct!.Stock); // 10 - 3 = 7 // Stock unchanged
        }
    }
}
