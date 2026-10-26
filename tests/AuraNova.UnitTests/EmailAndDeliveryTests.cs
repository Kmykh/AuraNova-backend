using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.BusinessSettings.DTOs;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Application.WhatsApp.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Notifications;
using AuraNova.Infrastructure.Notifications.Email;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AuraNova.UnitTests
{
    public class EmailAndDeliveryTests
    {
        private static AppDbContext GetDb() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private class RecordingQueue : IEmailQueue
        {
            public List<Guid> Ids { get; } = new();
            public void Enqueue(Guid notificationId) => Ids.Add(notificationId);
            public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private static readonly EmailSettings Configured = new() { Host = "smtp.test", Username = "tienda@test.pe", Password = "app-password" };

        private static NotificationService Notifications(AppDbContext db, IEmailQueue queue, EmailSettings settings)
        {
            var templates = new Mock<INotificationTemplateService>();
            templates.Setup(t => t.BuildQuoteReadyMessageAsync(It.IsAny<Order>())).ReturnsAsync("Tu cotización está lista");
            templates.Setup(t => t.BuildDeliveredMessageAsync(It.IsAny<Order>())).ReturnsAsync("Entregado");
            var whatsApp = new Mock<IWhatsAppMessageService>();
            whatsApp.Setup(w => w.NormalizePhone(It.IsAny<string>())).Returns("51987654321");
            whatsApp.Setup(w => w.GenerateUrlAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://wa.me/51987654321");

            return new NotificationService(db, templates.Object, whatsApp.Object, new NullLogger<NotificationService>(),
                new Mock<IConfiguration>().Object, queue, Options.Create(settings),
                Options.Create(new PublicSiteSettings { BaseUrl = "https://tienda.test" }));
        }

        private static async Task<Order> SeedOrder(AppDbContext db, string? email)
        {
            var customer = new Customer { Name = "María", Phone = "987654321", Email = email };
            var order = new Order { OrderCode = "AN-1001", Customer = customer, CustomerId = customer.Id, Status = OrderStatus.QuoteReady, Total = 80m };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order;
        }

        [Fact]
        public async Task Notify_WithEmailConfigured_QueuesEmailWithPaymentButton()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, "maria@example.com");
            var queue = new RecordingQueue();

            await Notifications(db, queue, Configured).NotifyAsync(order.Id, NotificationType.QuoteReady);

            var email = await db.Notifications.SingleAsync(n => n.Channel == NotificationChannel.Email);
            Assert.Equal("maria@example.com", email.Recipient);
            Assert.Equal($"https://tienda.test/pago/{order.Id}", email.ChannelUrl);
            Assert.Contains("cotización", email.Subject);
            Assert.Equal([email.Id], queue.Ids);
            Assert.Single(db.Notifications.Where(n => n.Channel == NotificationChannel.WhatsApp));
        }

        [Fact]
        public async Task Notify_WithoutSmtpOrEmail_OnlyCreatesWhatsApp()
        {
            using var db = GetDb();
            var withEmail = await SeedOrder(db, "maria@example.com");
            var queue = new RecordingQueue();

            await Notifications(db, queue, new EmailSettings()).NotifyAsync(withEmail.Id, NotificationType.QuoteReady);

            var noEmail = await SeedOrder(db, null);
            await Notifications(db, queue, Configured).NotifyAsync(noEmail.Id, NotificationType.QuoteReady);

            Assert.Empty(queue.Ids);
            Assert.DoesNotContain(db.Notifications, n => n.Channel == NotificationChannel.Email);
        }

        [Fact]
        public async Task Notify_Delivered_LinksToTracking()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, "maria@example.com");
            await Notifications(db, new RecordingQueue(), Configured).NotifyAsync(order.Id, NotificationType.OrderDelivered);

            var email = await db.Notifications.SingleAsync(n => n.Channel == NotificationChannel.Email);
            Assert.Equal($"https://tienda.test/seguimiento/{order.TrackingToken}", email.ChannelUrl);
        }

        [Theory]
        [InlineData(false, NotificationStatus.Sent)]
        [InlineData(true, NotificationStatus.Failed)]
        public async Task Dispatcher_RecordsSentOrFailed(bool fail, NotificationStatus expected)
        {
            using var db = GetDb();
            var order = await SeedOrder(db, "maria@example.com");
            var notification = new Notification
            {
                OrderId = order.Id, Type = NotificationType.QuoteReady, Channel = NotificationChannel.Email,
                Status = NotificationStatus.Generated, Recipient = "maria@example.com", Subject = "Asunto",
                Message = "Hola\n\nPaga aquí: https://tienda.test/pago/1", ChannelUrl = "https://tienda.test/pago/1"
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            var sender = new Mock<IEmailSender>();
            var send = sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()));
            if (fail) send.ThrowsAsync(new InvalidOperationException("SMTP caído"));
            else send.Returns(Task.CompletedTask);
            var settings = new Mock<IBusinessSettingsService>();
            settings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { BusinessName = "Aura Nova" });

            await new EmailNotificationDispatcher(db, sender.Object, settings.Object, new NullLogger<EmailNotificationDispatcher>())
                .DispatchAsync(notification.Id);

            var saved = await db.Notifications.FindAsync(notification.Id);
            Assert.Equal(expected, saved!.Status);
            if (fail) Assert.Contains("SMTP", saved.ErrorMessage);
            else sender.Verify(s => s.SendAsync("maria@example.com", "Asunto", It.Is<string>(h => h.Contains("Ir a pagar")), It.IsAny<string>(),
                It.Is<string?>(key => key != null && key.StartsWith(notification.Id.ToString("N"))), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task Resend_SendsJsonWithKey_AndExplainsErrors()
        {
            var handler = new CaptureHandler(System.Net.HttpStatusCode.OK, "{\"id\":\"abc\"}");
            var settings = Microsoft.Extensions.Options.Options.Create(new EmailSettings
            {
                ApiKey = "re_test", FromAddress = "pedidos@auranova.test", FromName = "Aura Nova", ReplyTo = "auranova@gmail.com"
            });
            Assert.True(settings.Value.UsesResend);
            Assert.True(settings.Value.IsConfigured);

            var sender = new ResendEmailSender(new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") }, settings);
            await sender.SendAsync("maria@example.com", "Asunto", "<p>Hola</p>", "Hola", "clave-1");

            Assert.Equal("https://api.resend.com/emails", handler.Request!.RequestUri!.ToString());
            Assert.Equal("Bearer re_test", handler.Request.Headers.Authorization!.ToString());
            Assert.Equal("clave-1", handler.Request.Headers.GetValues("Idempotency-Key").Single());
            var json = System.Text.Json.JsonDocument.Parse(handler.Body).RootElement;
            Assert.Equal("Aura Nova <pedidos@auranova.test>", json.GetProperty("from").GetString());
            Assert.Equal("auranova@gmail.com", json.GetProperty("reply_to").GetString());
            Assert.Equal("maria@example.com", json.GetProperty("to")[0].GetString());

            var failing = new ResendEmailSender(new HttpClient(new CaptureHandler(System.Net.HttpStatusCode.Forbidden,
                "{\"statusCode\":403,\"message\":\"The auranova.test domain is not verified.\"}")) { BaseAddress = new Uri("https://api.resend.com/") }, settings);
            var error = await Assert.ThrowsAsync<EmailSendException>(() => failing.SendAsync("maria@example.com", "A", "<p>B</p>", "B"));
            Assert.Contains("domain is not verified", error.Message);
        }

        [Fact]
        public async Task Settings_CleanPastedValues_AndBadKeyFailsWithoutLeakingIt()
        {
            var settings = new EmailSettings { ApiKey = " re_abc123\n", FromAddress = " pedidos@auranova.test ", FromName = "\"Aura Nova\"" };
            Assert.False(settings.ApiKeyLooksValid);
            settings.Normalize();
            Assert.Equal("re_abc123", settings.ApiKey);
            Assert.Equal("pedidos@auranova.test", settings.FromAddress);
            Assert.Equal("Aura Nova", settings.FromName);
            Assert.True(settings.ApiKeyLooksValid);

            var handler = new CaptureHandler(System.Net.HttpStatusCode.OK, "{}");
            var badKey = Microsoft.Extensions.Options.Options.Create(new EmailSettings { ApiKey = "sk_secreta", FromAddress = "pedidos@auranova.test" });
            var sender = new ResendEmailSender(new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") }, badKey);
            var error = await Assert.ThrowsAsync<EmailSendException>(() => sender.SendAsync("maria@example.com", "A", "<p>B</p>", "B"));
            Assert.Contains("re_", error.Message);
            Assert.DoesNotContain("sk_secreta", error.Message);
            Assert.Null(handler.Request);
        }

        private sealed class CaptureHandler(System.Net.HttpStatusCode status, string response) : HttpMessageHandler
        {
            public HttpRequestMessage? Request { get; private set; }
            public string Body { get; private set; } = string.Empty;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                Body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(status) { Content = new StringContent(response) };
            }
        }

        [Fact]
        public void Template_EscapesHtml_AndLinksUrls()
        {
            var html = EmailTemplate.Render("Aura Nova", "Hola", "Nombre: <script>alert(1)</script>\nVer: https://tienda.test/seguimiento/ABCD", "Ver mi pedido", "https://tienda.test");

            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("<a href=\"https://tienda.test/seguimiento/ABCD\"", html);
        }

        [Fact]
        public async Task Order_SavesRecipientAndAgency_AndValidatesDocument()
        {
            using var db = GetDb();
            var product = new Product { Name = "Ramo", Price = 50m, Stock = 5 };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var priceResolver = new Mock<IProductPriceResolver>();
            priceResolver.Setup(p => p.ResolvePriceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>()))
                .ReturnsAsync(new ProductPriceResolutionResult { EffectivePrice = 50m });
            var service = new OrderService(db, new Mock<INotificationService>().Object, new NullLogger<OrderService>(),
                new OrderStatusTransitionService(), new Mock<ICurrentUserService>().Object, priceResolver.Object);

            CreateOrderRequest Request(string document) => new()
            {
                Customer = new CreateOrderCustomerRequest { Name = "María", Phone = "987654321" },
                Items = [new() { ProductId = product.Id, Quantity = 1 }],
                Delivery = new CreateOrderDeliveryRequest
                {
                    Type = "NationalShipping", Department = "Lima", Province = "Lima", District = "Miraflores",
                    DeliveryAddress = "Agencia Shalom Av. Arequipa", RecipientName = "Ana Pérez", RecipientPhone = "912 345 678",
                    RecipientDocument = document, PreferredAgency = "shalom"
                }
            };

            var created = await service.CreateAsync(Request("4567-8901"));
            var order = await db.Orders.FindAsync(created.Id);
            Assert.Equal("Ana Pérez", order!.RecipientName);
            Assert.Equal("912345678", order.RecipientPhone);
            Assert.Equal("45678901", order.RecipientDocument);
            Assert.Equal("Shalom", order.PreferredAgency);

            await Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(Request("123")));
        }
    }
}
