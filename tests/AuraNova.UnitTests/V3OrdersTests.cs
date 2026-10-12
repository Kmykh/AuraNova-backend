using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class V3OrdersTests
    {
        private static AppDbContext GetInMemoryDb() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static OrderService CreateOrderService(AppDbContext db) =>
            new(db, new Mock<INotificationService>().Object, new NullLogger<OrderService>(), new OrderStatusTransitionService(),
                new Mock<ICurrentUserService>().Object, new Mock<AuraNova.Application.Orders.Interfaces.IProductPriceResolver>().Object);

        private static CreateCustomOrderRequest CustomRequest() => new()
        {
            Customer = new CreateOrderCustomerRequest { Name = "Ana Torres", Phone = "987654321" },
            Delivery = new CreateOrderDeliveryRequest { Type = "NationalShipping", Department = "Junín", Province = "Huancayo", District = "El Tambo" },
            CustomizationNotes = "Ramo en tonos lila con mariposas"
        };

        // ---------- Códigos ----------

        [Fact]
        public void TrackingCode_IsEightUnambiguousCharsWithDash()
        {
            for (var i = 0; i < 200; i++)
            {
                var code = OrderCodes.NewTrackingCode();
                Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code);
                Assert.DoesNotContain(code.Replace("-", ""), c => "01OIL".Contains(c));
            }
        }

        [Fact]
        public void TrackingCodes_AreNotRepeated()
        {
            var codes = Enumerable.Range(0, 5000).Select(_ => OrderCodes.NewTrackingCode()).ToHashSet();
            Assert.Equal(5000, codes.Count);
        }

        [Theory]
        [InlineData("K7M3-Q9XA", "K7M3-Q9XA")]
        [InlineData("k7m3q9xa", "K7M3-Q9XA")]
        [InlineData("  K7M3 - Q9XA ", "K7M3-Q9XA")]
        [InlineData("https://www.auranovaoficial.com/seguimiento/K7M3-Q9XA", "K7M3-Q9XA")]
        [InlineData("https://www.auranovaoficial.com/seguimiento/k7m3-q9xa/", "K7M3-Q9XA")]
        public void NormalizeTrackingCode_AcceptsWhatCustomersType(string input, string expected)
        {
            Assert.Equal(expected, OrderCodes.NormalizeTrackingCode(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("K7M3-Q9X")]        // corto
        [InlineData("K7M3-Q9XAA")]      // largo
        [InlineData("K7M0-Q9XA")]       // 0 no existe en el alfabeto
        [InlineData("AN-1001")]
        [InlineData("'; DROP TABLE--")]
        public void NormalizeTrackingCode_RejectsInvalidInput(string? input)
        {
            Assert.Null(OrderCodes.NormalizeTrackingCode(input));
        }

        [Fact]
        public async Task OrderNumbers_StartAt1001AndIncrement()
        {
            using var db = GetInMemoryDb();
            var service = CreateOrderService(db);

            var first = await service.CreateCustomAsync(CustomRequest());
            var second = await service.CreateCustomAsync(CustomRequest());

            Assert.Equal("AN-1001", first.OrderCode);
            Assert.Equal("AN-1002", second.OrderCode);
            Assert.NotEqual(first.TrackingToken, second.TrackingToken);
        }

        // ---------- Seguimiento con un solo código ----------

        [Fact]
        public async Task TrackingByCode_FindsOrderWithLooseInput_AndHidesToken()
        {
            using var db = GetInMemoryDb();
            var created = await CreateOrderService(db).CreateCustomAsync(CustomRequest());
            var tracking = new OrderTrackingService(db, new TestStorage());

            var result = await tracking.GetTrackingByCodeAsync(created.TrackingToken.ToLowerInvariant().Replace("-", " "));

            Assert.NotNull(result);
            Assert.Equal(created.OrderCode, result!.OrderCode);
            Assert.Equal(created.Id, result.OrderId);
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.DoesNotContain(created.TrackingToken, json);
            Assert.DoesNotContain("987654321", json);
        }

        [Fact]
        public async Task TrackingByCode_UnknownCode_ReturnsNull()
        {
            using var db = GetInMemoryDb();
            await CreateOrderService(db).CreateCustomAsync(CustomRequest());
            var tracking = new OrderTrackingService(db, new TestStorage());

            Assert.Null(await tracking.GetTrackingByCodeAsync("ZZZZ-ZZZZ"));
            Assert.Null(await tracking.GetTrackingByCodeAsync("no-es-un-codigo"));
        }

        // ---------- Brief del pedido personalizado ----------

        [Fact]
        public async Task CustomOrder_SavesGalleryLinksDateOccasionAndBudget()
        {
            using var db = GetInMemoryDb();
            var request = CustomRequest();
            request.ReferenceLinks = ["https://pin.it/abc123", " https://www.tiktok.com/@aura/video/1 ", "https://pin.it/abc123"];
            request.DesiredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            request.Occasion = "Aniversario";
            request.BudgetRange = "S/ 80 - 120";

            var created = await CreateOrderService(db).CreateCustomAsync(request,
                ["private/custom-orders/a.webp", "private/custom-orders/b.webp"]);

            var order = await db.Orders.Include(o => o.ReferenceImages).SingleAsync(o => o.Id == created.Id);
            Assert.Equal("private/custom-orders/a.webp", order.ReferenceImageUrl);
            Assert.Equal(["private/custom-orders/a.webp", "private/custom-orders/b.webp"],
                order.ReferenceImages!.OrderBy(i => i.SortOrder).Select(i => i.Path));
            Assert.Equal(["https://pin.it/abc123", "https://www.tiktok.com/@aura/video/1"], order.ReferenceLinks);
            Assert.Equal("Aniversario", order.Occasion);
            Assert.Equal("S/ 80 - 120", order.BudgetRange);

            var tracking = await new OrderTrackingService(db, new TestStorage()).GetTrackingByCodeAsync(created.TrackingToken);
            Assert.Equal(2, tracking!.ReferenceImages.Count);
            Assert.All(tracking.ReferenceImages, url => Assert.StartsWith(TestStorage.SignedBase, url));
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("ftp://archivo.com/x")]
        [InlineData("no es un enlace")]
        public async Task CustomOrder_RejectsInvalidLinks(string link)
        {
            using var db = GetInMemoryDb();
            var request = CustomRequest();
            request.ReferenceLinks = [link];

            await Assert.ThrowsAsync<OrderValidationException>(() => CreateOrderService(db).CreateCustomAsync(request));
        }

        [Fact]
        public async Task CustomOrder_RejectsPastDesiredDate()
        {
            using var db = GetInMemoryDb();
            var request = CustomRequest();
            request.DesiredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));

            await Assert.ThrowsAsync<OrderValidationException>(() => CreateOrderService(db).CreateCustomAsync(request));
        }

        [Fact]
        public async Task CustomOrder_RejectsMoreThanFiveImages()
        {
            using var db = GetInMemoryDb();
            var paths = Enumerable.Range(1, 6).Select(i => $"private/custom-orders/{i}.webp").ToList();

            await Assert.ThrowsAsync<OrderValidationException>(() => CreateOrderService(db).CreateCustomAsync(CustomRequest(), paths));
        }
    }
}
