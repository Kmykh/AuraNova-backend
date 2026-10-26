using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Payments;
using AuraNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraNova.IntegrationTests
{
    /// <summary>Webhook del tópico Order de Mercado Pago: firma, lectura de data.id y confirmación del pago.</summary>
    public class MercadoPagoWebhookTests
    {
        private const string Secret = "webhook-test-secret";
        private const string MpOrderId = "ORDTST01WEBHOOK";

        /// <summary>Mercado Pago simulado: la order ya figura como pagada.</summary>
        private class ApprovedGateway(string externalReference) : IMercadoPagoGateway
        {
            public int Lookups { get; private set; }

            public Task<GatewayOrder> CreateOrderAsync(GatewayCreateOrder request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<GatewayOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
            {
                Lookups++;
                return Task.FromResult<GatewayOrder?>(orderId != MpOrderId ? null : new GatewayOrder(
                    MpOrderId, "processed", "accredited", externalReference, 50m,
                    [new GatewayPayment("PAY01", "processed", "accredited", "visa", "credit_card")]));
            }

            public Task<GatewayOrder?> FindByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default) =>
                Task.FromResult<GatewayOrder?>(null);

            public Task<GatewayOrder> RefundAsync(string orderId, string? paymentId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<GatewayOrder> CancelAsync(string orderId, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }

        private static (WebApplicationFactory<Program> Factory, ApprovedGateway Gateway, Guid OrderId) Setup()
        {
            var dbName = Guid.NewGuid().ToString();
            var transactionId = Guid.NewGuid();
            var gateway = new ApprovedGateway(transactionId.ToString("N"));

            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JwtSettings:SecretKey", "TestSecretKeyForIntegrationTestsMustBeLongEnough123456");
                builder.UseSetting("JwtSettings:Issuer", "AuraNova.Test");
                builder.UseSetting("JwtSettings:Audience", "AuraNova.Test");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
                builder.UseSetting("InitialAdmin:Enabled", "false");
                builder.UseSetting("MercadoPago:WebhookSecret", Secret);

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);
                    services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));

                    var gatewayDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMercadoPagoGateway));
                    if (gatewayDescriptor != null) services.Remove(gatewayDescriptor);
                    services.AddSingleton<IMercadoPagoGateway>(gateway);
                });
            });

            // Pedido con un adelanto "en proceso" esperando la confirmación de Mercado Pago
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer { Name = "María", Phone = "987654321" };
            var order = new Order
            {
                OrderCode = "AN-1001", Customer = customer, CustomerId = customer.Id,
                Status = OrderStatus.WaitingPayment, DeliveryType = DeliveryType.Delivery, Subtotal = 100m, Total = 100m
            };
            var payment = new Payment { OrderId = order.Id, Amount = 100m };
            db.Orders.Add(order);
            db.Payments.Add(payment);
            db.PaymentTransactions.Add(new PaymentTransaction
            {
                Id = transactionId, PaymentId = payment.Id, OrderId = order.Id, MethodId = "visa", MethodType = "credit_card",
                Amount = 50m, IsDeposit = true, Status = "processing", ExternalOrderId = MpOrderId
            });
            db.SaveChanges();

            return (factory, gateway, order.Id);
        }

        private static HttpRequestMessage Notification(string dataId, string type, string? signatureSecret)
        {
            const string requestId = "req-integration-1";
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
            var hash = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(signatureSecret ?? "otra-firma"), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();

            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/mercadopago?data.id={dataId}&type={type}")
            {
                Content = new StringContent(
                    "{\"action\":\"order.processed\",\"type\":\"" + type + "\",\"data\":{\"id\":\"" + dataId + "\"}}",
                    Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-signature", $"ts={ts},v1={hash}");
            request.Headers.Add("x-request-id", requestId);
            return request;
        }

        private static async Task<List<string>> WebhookResults(WebApplicationFactory<Program> factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.PaymentWebhookEvents.OrderBy(e => e.ReceivedAt).Select(e => e.Result).ToListAsync();
        }

        private static async Task<(Order Order, Payment Payment)> Load(WebApplicationFactory<Program> factory, Guid orderId)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.Include(o => o.Payment).SingleAsync(o => o.Id == orderId);
            return (order, order.Payment!);
        }

        [Fact]
        public async Task SignedNotification_ConfirmsTheDeposit_AndRepeatsCountOnce()
        {
            var (factory, gateway, orderId) = Setup();
            using var client = factory.CreateClient();

            var first = await client.SendAsync(Notification(MpOrderId, "order", Secret));
            var repeated = await client.SendAsync(Notification(MpOrderId, "order", Secret));

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            Assert.Equal(2, gateway.Lookups);

            var (order, payment) = await Load(factory, orderId);
            Assert.Equal(OrderStatus.PaymentConfirmed, order.Status);
            Assert.Equal(50m, payment.AmountPaid);
            Assert.Equal(["processed", "processed"], await WebhookResults(factory));
        }

        [Fact]
        public async Task WrongSignature_IsRejected_AndNothingChanges()
        {
            var (factory, gateway, orderId) = Setup();
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Notification(MpOrderId, "order", signatureSecret: null));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, gateway.Lookups);
            var (order, payment) = await Load(factory, orderId);
            Assert.Equal(OrderStatus.WaitingPayment, order.Status);
            Assert.Equal(0m, payment.AmountPaid);
            Assert.Equal(["invalid_signature"], await WebhookResults(factory));
        }

        [Fact]
        public async Task Config_ReturnsThePublicKey_FromTheBackendSettings()
        {
            var (factory, _, _) = Setup();
            using var withKey = factory.WithWebHostBuilder(b => b.UseSetting("MercadoPago:PublicKey", "APP_USR-test-public"));
            using var client = withKey.CreateClient();

            var json = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/payments/mercadopago/config");

            Assert.Equal("APP_USR-test-public", json.GetProperty("publicKey").GetString());
        }

        [Fact]
        public async Task OtherTopicsAndUnknownOrders_AreAcknowledged()
        {
            var (factory, _, orderId) = Setup();
            using var client = factory.CreateClient();

            // "Simular notificación" del panel usa ids que no existen: se responde 200 sin tocar nada
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Notification("ORDTST01NOEXISTE", "order", Secret))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Notification("12345", "chargebacks", Secret))).StatusCode);

            var (order, _) = await Load(factory, orderId);
            Assert.Equal(OrderStatus.WaitingPayment, order.Status);
        }
    }
}
