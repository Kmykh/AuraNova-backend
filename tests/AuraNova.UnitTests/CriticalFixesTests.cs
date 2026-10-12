using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using AuraNova.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class CriticalFixesTests
    {
        private static AppDbContext GetInMemoryDb() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // ---------- Private bucket / signed URLs ----------

        private class RoutingHandler : HttpMessageHandler
        {
            public List<(HttpMethod Method, string Uri, string? Body)> Requests { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add((request.Method, request.RequestUri!.ToString(), body));

                if (request.RequestUri!.AbsolutePath.EndsWith("/object/sign/customer-files"))
                {
                    var json = """
                        [{"error":null,"path":"payment-evidence/1/a.webp","signedURL":"/object/sign/customer-files/payment-evidence/1/a.webp?token=abc"}]
                        """;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        private static SupabaseStorageService CreateStorage(RoutingHandler handler) =>
            new(new HttpClient(handler),
                Options.Create(new SupabaseSettings { Url = "https://test.supabase.co", ServiceRoleKey = "fake-key" }),
                new NullLogger<SupabaseStorageService>(),
                TestUrlResolver.Instance);

        [Fact]
        public async Task Upload_CatalogFolder_StaysInPublicBucket()
        {
            var handler = new RoutingHandler();
            var storage = CreateStorage(handler);

            var path = await storage.UploadAsync(new MemoryStream([1]), "a.webp", "image/webp", "products");

            Assert.StartsWith("products/", path);
            Assert.Contains("/object/payment-evidence/products/", handler.Requests.Single().Uri);
        }

        [Theory]
        [InlineData("shipping-evidence")]
        [InlineData("custom-orders")]
        [InlineData("payment-evidence/123")]
        public async Task Upload_CustomerFolders_GoToPrivateBucket(string folder)
        {
            var handler = new RoutingHandler();
            var storage = CreateStorage(handler);

            var path = await storage.UploadAsync(new MemoryStream([1]), "a.webp", "image/webp", folder);

            Assert.StartsWith($"private/{folder}/", path);
            Assert.Contains($"/object/customer-files/{folder}/", handler.Requests.Single().Uri);
        }

        [Fact]
        public async Task GetReadUrls_SignsPrivateAndBuildsPublicInOneCall()
        {
            var handler = new RoutingHandler();
            var storage = CreateStorage(handler);

            var urls = await storage.GetReadUrlsAsync(["private/payment-evidence/1/a.webp", "products/p.webp", "https://cdn.example/x.png"]);

            Assert.Equal("https://test.supabase.co/storage/v1/object/sign/customer-files/payment-evidence/1/a.webp?token=abc", urls["private/payment-evidence/1/a.webp"]);
            Assert.Equal($"{TestUrlResolver.PublicBase}/products/p.webp", urls["products/p.webp"]);
            Assert.Equal("https://cdn.example/x.png", urls["https://cdn.example/x.png"]);

            var sign = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, sign.Method);
            Assert.Contains("\"paths\":[\"payment-evidence/1/a.webp\"]", sign.Body);
        }

        [Fact]
        public async Task Delete_PrivatePath_TargetsPrivateBucket()
        {
            var handler = new RoutingHandler();
            var storage = CreateStorage(handler);

            await storage.DeleteAsync("private/shipping-evidence/a.webp");

            Assert.EndsWith("/object/customer-files/shipping-evidence/a.webp", handler.Requests.Single().Uri);
        }

        // ---------- Stock ----------

        [Fact]
        public async Task ProductStock_ReserveAndRelease()
        {
            using var db = GetInMemoryDb();
            var product = new Product { Name = "Ramo", Price = 10, Stock = 1 };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            Assert.True(await ProductStock.TryReserveAsync(db, product.Id, 1));
            Assert.False(await ProductStock.TryReserveAsync(db, product.Id, 1)); // last unit already taken

            await ProductStock.ReleaseAsync(db, product.Id, 1);
            Assert.Equal(1, product.Stock);
        }

        // ---------- Unpaid order expiration ----------

        private static async Task<(Order Order, Product Product)> SeedWaitingPaymentOrder(AppDbContext db, DateTimeOffset createdAt,
            bool isCustom = false, PaymentStatus paymentStatus = PaymentStatus.Pending)
        {
            var customer = new Customer { Name = "Ana", Phone = "999" };
            var product = new Product { Name = "Ramo", Price = 10, Stock = 4 };
            var order = new Order
            {
                OrderCode = $"PED-{Random.Shared.Next(100000, 999999)}",
                CustomerId = customer.Id,
                DeliveryType = DeliveryType.MeetingPoint,
                Status = OrderStatus.WaitingPayment,
                IsCustomOrder = isCustom,
                CreatedAt = createdAt,
                Items = isCustom ? [] : [new OrderItem { ProductId = product.Id, Quantity = 2, UnitPrice = 10, Subtotal = 20 }]
            };
            db.AddRange(customer, product, order, new Payment { OrderId = order.Id, Amount = 20, Status = paymentStatus });
            await db.SaveChangesAsync();
            return (order, product);
        }

        private static UnpaidOrderExpirationService CreateExpirationService(AppDbContext db, int hours)
        {
            var statusService = new OrderStatusService(db, new OrderStatusTransitionService(),
                new Mock<INotificationService>().Object, new Mock<ICurrentUserService>().Object, new NullLogger<OrderStatusService>());
            return new UnpaidOrderExpirationService(db, statusService,
                Options.Create(new OrderExpirationSettings { UnpaidExpirationHours = hours }),
                new NullLogger<UnpaidOrderExpirationService>());
        }

        [Fact]
        public async Task Expiration_Disabled_DoesNothing()
        {
            using var db = GetInMemoryDb();
            var now = DateTimeOffset.UtcNow;
            var (order, _) = await SeedWaitingPaymentOrder(db, now.AddDays(-5));

            var expired = await CreateExpirationService(db, hours: 0).ExpireAsync(now);

            Assert.Equal(0, expired);
            Assert.Equal(OrderStatus.WaitingPayment, (await db.Orders.FindAsync(order.Id))!.Status);
        }

        [Fact]
        public async Task Expiration_OldUnpaidOrder_IsCancelledAndStockReleased()
        {
            using var db = GetInMemoryDb();
            var now = DateTimeOffset.UtcNow;
            var (order, product) = await SeedWaitingPaymentOrder(db, now.AddHours(-30));

            var expired = await CreateExpirationService(db, hours: 24).ExpireAsync(now);

            Assert.Equal(1, expired);
            Assert.Equal(OrderStatus.Cancelled, (await db.Orders.FindAsync(order.Id))!.Status);
            Assert.Equal(6, (await db.Products.FindAsync(product.Id))!.Stock);
        }

        [Fact]
        public async Task Expiration_SkipsRecentReportedAndCustomOrders()
        {
            using var db = GetInMemoryDb();
            var now = DateTimeOffset.UtcNow;
            var (recent, _) = await SeedWaitingPaymentOrder(db, now.AddHours(-2));
            var (reported, _) = await SeedWaitingPaymentOrder(db, now.AddHours(-30), paymentStatus: PaymentStatus.Reported);
            var (custom, _) = await SeedWaitingPaymentOrder(db, now.AddHours(-30), isCustom: true);

            var expired = await CreateExpirationService(db, hours: 24).ExpireAsync(now);

            Assert.Equal(0, expired);
            foreach (var id in new[] { recent.Id, reported.Id, custom.Id })
                Assert.Equal(OrderStatus.WaitingPayment, (await db.Orders.FindAsync(id))!.Status);
        }
    }
}
