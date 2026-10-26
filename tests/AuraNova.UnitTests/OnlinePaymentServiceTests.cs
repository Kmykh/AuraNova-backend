using System.Security.Cryptography;
using System.Text;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.OnlinePayments.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Payments;
using AuraNova.Infrastructure.Persistence;
using MercadoPago.Error;
using MercadoPago.Webhook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AuraNova.UnitTests
{
    public class OnlinePaymentServiceTests
    {
        /// <summary>Mercado Pago simulado: devuelve lo que la prueba indique y recuerda lo que se le envió.</summary>
        private class FakeGateway : IMercadoPagoGateway
        {
            public List<GatewayCreateOrder> Created { get; } = new();
            public Func<GatewayCreateOrder, GatewayOrder> OnCreate { get; set; } = Approved;
            public Dictionary<string, GatewayOrder> Orders { get; } = new();

            public Task<GatewayOrder> CreateOrderAsync(GatewayCreateOrder request, CancellationToken cancellationToken = default)
            {
                Created.Add(request);
                var order = OnCreate(request);
                if (order.Id != null) Orders[order.Id] = order;
                return Task.FromResult(order);
            }

            public Task<GatewayOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Orders.GetValueOrDefault(orderId));

            public Task<GatewayOrder?> FindByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default) =>
                Task.FromResult(Orders.Values.FirstOrDefault(o => o.ExternalReference == externalReference));

            public List<(string OrderId, string? PaymentId, decimal? Amount)> Refunds { get; } = new();

            public Task<GatewayOrder> RefundAsync(string orderId, string? paymentId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken = default)
            {
                Refunds.Add((orderId, paymentId, amount));
                return Task.FromResult(Orders[orderId] with { Status = amount == null ? "refunded" : "processed" });
            }

            public List<string> Cancelled { get; } = new();

            public Task<GatewayOrder> CancelAsync(string orderId, CancellationToken cancellationToken = default)
            {
                Cancelled.Add(orderId);
                var order = Orders[orderId] with { Status = "canceled", StatusDetail = "canceled", Payments = [] };
                Orders[orderId] = order;
                return Task.FromResult(order);
            }

            public static GatewayOrder Approved(GatewayCreateOrder r) =>
                new($"ORD-{r.IdempotencyKey}", "processed", "accredited", r.ExternalReference, r.Amount,
                    [new GatewayPayment("PAY-1", "processed", "accredited", r.PaymentMethodId, r.PaymentMethodType)]);

            public static GatewayOrder Failed(GatewayCreateOrder r) =>
                new($"ORD-{r.IdempotencyKey}", "failed", "failed", r.ExternalReference, 0,
                    [new GatewayPayment("PAY-1", "failed", "insufficient_amount", r.PaymentMethodId, r.PaymentMethodType)]);

            public static GatewayOrder Processing(GatewayCreateOrder r) =>
                new($"ORD-{r.IdempotencyKey}", "processing", "in_process", r.ExternalReference, 0,
                    [new GatewayPayment("PAY-1", "processing", "in_process", r.PaymentMethodId, r.PaymentMethodType)]);
        }

        private static AppDbContext GetDb() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static async Task<Order> SeedOrder(AppDbContext db, decimal total = 100m, OrderStatus status = OrderStatus.WaitingPayment)
        {
            var customer = new Customer { Name = "María", Phone = "987654321", Email = "maria@example.com" };
            var order = new Order
            {
                OrderCode = "AN-1001",
                Customer = customer,
                CustomerId = customer.Id,
                Status = status,
                DeliveryType = DeliveryType.Delivery,
                Subtotal = total,
                Total = total
            };
            db.Orders.Add(order);
            db.Payments.Add(new Payment { OrderId = order.Id, Amount = total });
            await db.SaveChangesAsync();
            return order;
        }

        private static (OnlinePaymentService Service, FakeGateway Gateway, Mock<INotificationService> Notifications) Build(AppDbContext db)
        {
            var gateway = new FakeGateway();
            var notifications = new Mock<INotificationService>();
            return (new OnlinePaymentService(db, gateway, notifications.Object, new NullLogger<OnlinePaymentService>()), gateway, notifications);
        }

        private static CreateOnlinePaymentRequest Card(bool payFull, string token = "tok-1") => new()
        {
            Method = "card",
            Token = token,
            PaymentMethodId = "visa",
            PaymentTypeId = "credit_card",
            Installments = 1,
            PayFull = payFull,
            PayerEmail = "pagador@example.com",
            IdentificationType = "DNI",
            IdentificationNumber = "12345678"
        };

        [Fact]
        public async Task Deposit_ChargesHalf_ConfirmsOrder_AndLeavesBalance()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 85.50m);
            var (service, gateway, notifications) = Build(db);

            var result = await service.PayAsync(order.Id, Card(payFull: false));

            Assert.Equal("approved", result.Status);
            Assert.Equal(42.75m, gateway.Created.Single().Amount);
            Assert.Equal(42.75m, result.AmountPaid);
            Assert.Equal(42.75m, result.BalanceDue);
            Assert.Equal(OrderStatus.PaymentConfirmed, (await db.Orders.FindAsync(order.Id))!.Status);

            var payment = await db.Payments.SingleAsync();
            Assert.Equal(PaymentStatus.Confirmed, payment.Status);
            Assert.Equal(PaymentMethod.MercadoPago, payment.Method);
            Assert.Contains(db.OrderStatusHistory, h => h.Comment!.Contains("Saldo pendiente: S/ 42.75"));
            notifications.Verify(n => n.NotifyAsync(order.Id, NotificationType.PaymentConfirmed, null), Times.Once);
        }

        [Fact]
        public async Task SecondPayment_ChargesExactlyTheBalance_EvenIfOrderIsInWorkshop()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 100m);
            var (service, gateway, _) = Build(db);
            await service.PayAsync(order.Id, Card(payFull: false, token: "tok-1"));

            order.Status = OrderStatus.Preparing;
            await db.SaveChangesAsync();

            // Aunque pida "50%", el segundo pago siempre es el saldo
            var result = await service.PayAsync(order.Id, Card(payFull: false, token: "tok-2"));

            Assert.Equal(50m, gateway.Created[1].Amount);
            Assert.Equal(100m, result.AmountPaid);
            Assert.Equal(0m, result.BalanceDue);
            Assert.Equal(OrderStatus.Preparing, (await db.Orders.FindAsync(order.Id))!.Status);

            var ex = await Assert.ThrowsAsync<OnlinePaymentException>(() => service.PayAsync(order.Id, Card(true, "tok-3")));
            Assert.Contains("ya está pagado", ex.Message);
        }

        [Fact]
        public async Task FullPayment_ChargesTotal()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 120m);
            var (service, gateway, _) = Build(db);

            var result = await service.PayAsync(order.Id, Card(payFull: true));

            Assert.Equal(120m, gateway.Created.Single().Amount);
            Assert.Equal(0m, result.BalanceDue);
            Assert.Contains("está pagado", result.Message);
        }

        [Fact]
        public async Task SendsBuyerProductsAndDevice_ForMercadoPagoQuality()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 100m);
            order.Customer!.Name = "María López Pérez";
            order.DeliveryAddress = "Jr. Puno 450";
            order.District = "El Tambo";
            order.Department = "Junín";
            await db.SaveChangesAsync();
            var (service, gateway, _) = Build(db);

            var request = Card(payFull: false);
            request.DeviceId = "armor.abc123";
            await service.PayAsync(order.Id, request);

            var sent = gateway.Created.Single();
            Assert.Equal("María", sent.PayerFirstName);
            Assert.Equal("López Pérez", sent.PayerLastName);
            Assert.Equal("987654321", sent.PhoneNumber);
            Assert.Equal("armor.abc123", sent.DeviceId);
            Assert.Equal("Jr. Puno 450", sent.PayerAddress?.StreetName);
            Assert.Equal("Jr. Puno 450", sent.ShipmentAddress?.StreetName);
            Assert.Equal(50m, sent.Items!.Sum(i => i.Quantity * i.UnitPrice));
            Assert.Null(sent.History?.LastPurchaseAt);
            Assert.Contains("adelanto 50%", sent.Description);
        }

        [Fact]
        public async Task Rejected_KeepsOrderWaitingPayment_WithFriendlyMessage()
        {
            using var db = GetDb();
            var order = await SeedOrder(db);
            var (service, gateway, notifications) = Build(db);
            gateway.OnCreate = FakeGateway.Failed;

            var result = await service.PayAsync(order.Id, Card(payFull: true));

            Assert.Equal("rejected", result.Status);
            Assert.Contains("fondos insuficientes", result.Message);
            Assert.Equal(0m, result.AmountPaid);
            Assert.Equal(OrderStatus.WaitingPayment, (await db.Orders.FindAsync(order.Id))!.Status);
            notifications.Verify(n => n.NotifyAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Processing_IsConfirmedLaterByWebhook_AndRepeatedWebhooksCountOnce()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 60m);
            var (service, gateway, _) = Build(db);
            gateway.OnCreate = FakeGateway.Processing;

            var result = await service.PayAsync(order.Id, Card(payFull: true));
            Assert.Equal("pending", result.Status);

            // No se permite otro intento mientras ese pago está en proceso
            await Assert.ThrowsAsync<OnlinePaymentException>(() => service.PayAsync(order.Id, Card(true, "tok-2")));

            var mpOrderId = gateway.Orders.Keys.Single();
            var created = gateway.Created.Single();
            gateway.Orders[mpOrderId] = FakeGateway.Approved(created) with { Id = mpOrderId };

            await service.HandleOrderNotificationAsync(mpOrderId);
            await service.HandleOrderNotificationAsync(mpOrderId);

            var payment = await db.Payments.SingleAsync();
            Assert.Equal(60m, payment.AmountPaid);
            Assert.Equal(OrderStatus.PaymentConfirmed, (await db.Orders.FindAsync(order.Id))!.Status);
            Assert.Single(db.OrderStatusHistory.Where(h => h.Comment!.Contains("Mercado Pago")));
        }

        [Fact]
        public async Task ReturningBuyer_IsRecognizedByPhone_ForMercadoPagoHistory()
        {
            using var db = GetDb();
            // Pedido anterior pagado: cada pedido crea su propio cliente, con el mismo celular
            var firstVisit = DateTimeOffset.UtcNow.AddMonths(-2);
            var previousCustomer = new Customer { Name = "María", Phone = "987654321", CreatedAt = firstVisit };
            var previous = new Order
            {
                OrderCode = "AN-0999", Customer = previousCustomer, CustomerId = previousCustomer.Id, CreatedAt = firstVisit,
                Status = OrderStatus.Delivered, DeliveryType = DeliveryType.Delivery, Subtotal = 40m, Total = 40m
            };
            db.Orders.Add(previous);
            db.Payments.Add(new Payment { OrderId = previous.Id, Amount = 40m, AmountPaid = 40m, Status = PaymentStatus.Confirmed });
            await db.SaveChangesAsync();

            var order = await SeedOrder(db, total: 100m);
            var (service, gateway, _) = Build(db);
            await service.PayAsync(order.Id, Card(payFull: true));

            var history = gateway.Created.Single().History!;
            Assert.Equal(firstVisit, history.RegisteredAt);
            Assert.Equal(firstVisit, history.LastPurchaseAt);
        }

        [Fact]
        public async Task Refund_Partial_ThenRest_ReducesWhatWasPaid()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 100m);
            var (service, gateway, _) = Build(db);
            await service.PayAsync(order.Id, Card(payFull: true));
            var transaction = await db.PaymentTransactions.SingleAsync();

            var partial = await service.RefundAsync(order.Id, transaction.Id, new RefundOnlinePaymentRequest { Amount = 30m, Reason = "Faltó un globo" });

            Assert.Equal(70m, partial.AmountPaid);
            Assert.Equal(30m, partial.BalanceDue);
            Assert.Equal(("PAY-1", 30m), (gateway.Refunds.Single().PaymentId, gateway.Refunds.Single().Amount));
            Assert.Contains(db.OrderStatusHistory, h => h.Comment!.Contains("Devolución de S/ 30.00") && h.Comment.Contains("Faltó un globo"));

            // Sin monto se devuelve lo que queda (70), como devolución total en Mercado Pago
            var rest = await service.RefundAsync(order.Id, transaction.Id, new RefundOnlinePaymentRequest());
            Assert.Equal(70m, rest.Refunded);
            Assert.Equal(0m, rest.AmountPaid);
            Assert.Null(gateway.Refunds[1].Amount);
            Assert.Equal("refunded", (await db.PaymentTransactions.SingleAsync()).Status);

            var ex = await Assert.ThrowsAsync<OnlinePaymentException>(() => service.RefundAsync(order.Id, transaction.Id, new RefundOnlinePaymentRequest()));
            Assert.Contains("devuelto por completo", ex.Message);
        }

        [Fact]
        public async Task Refund_MoreThanCharged_OrNotApproved_IsRejected()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 100m);
            var (service, gateway, _) = Build(db);
            await service.PayAsync(order.Id, Card(payFull: false));
            var approved = await db.PaymentTransactions.SingleAsync();

            var tooMuch = await Assert.ThrowsAsync<OnlinePaymentException>(() =>
                service.RefundAsync(order.Id, approved.Id, new RefundOnlinePaymentRequest { Amount = 60m }));
            Assert.Contains("como máximo S/ 50.00", tooMuch.Message);

            gateway.OnCreate = FakeGateway.Failed;
            await service.PayAsync(order.Id, Card(payFull: true, token: "tok-2"));
            var rejected = await db.PaymentTransactions.SingleAsync(t => t.Id != approved.Id);
            await Assert.ThrowsAsync<OnlinePaymentException>(() => service.RefundAsync(order.Id, rejected.Id, new RefundOnlinePaymentRequest()));
            Assert.Empty(gateway.Refunds);
        }

        [Fact]
        public async Task PaymentApprovedAfterOrderWasCancelled_IsRefundedAutomatically()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 80m);
            var (service, gateway, notifications) = Build(db);
            gateway.OnCreate = FakeGateway.Processing;
            await service.PayAsync(order.Id, Card(payFull: true));

            order.Status = OrderStatus.Cancelled;
            await db.SaveChangesAsync();
            var mpOrderId = gateway.Orders.Keys.Single();
            gateway.Orders[mpOrderId] = FakeGateway.Approved(gateway.Created.Single()) with { Id = mpOrderId };

            await service.HandleOrderNotificationAsync(mpOrderId);

            Assert.Equal(mpOrderId, gateway.Refunds.Single().OrderId);
            Assert.Equal(0m, (await db.Payments.SingleAsync()).AmountPaid);
            Assert.Equal(OrderStatus.Cancelled, (await db.Orders.FindAsync(order.Id))!.Status);
            Assert.Contains(db.OrderStatusHistory, h => h.Comment!.Contains("se devolvió automáticamente"));
            notifications.Verify(n => n.NotifyAsync(order.Id, NotificationType.PaymentConfirmed, null), Times.Never);
        }

        [Fact]
        public async Task RefundMadeInMercadoPagoPanel_IsSyncedByWebhook()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 100m);
            var (service, gateway, _) = Build(db);
            await service.PayAsync(order.Id, Card(payFull: true));

            var mpOrderId = gateway.Orders.Keys.Single();
            gateway.Orders[mpOrderId] = gateway.Orders[mpOrderId] with { Status = "refunded", StatusDetail = "refunded", Payments = [] };
            await service.HandleOrderNotificationAsync(mpOrderId);
            await service.HandleOrderNotificationAsync(mpOrderId);

            Assert.Equal(0m, (await db.Payments.SingleAsync()).AmountPaid);
            Assert.Equal(100m, (await db.PaymentTransactions.SingleAsync()).RefundedAmount);
        }

        [Fact]
        public async Task Reconcile_ConfirmsPaymentsWhoseWebhookNeverArrived()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 60m);
            var (service, gateway, _) = Build(db);
            gateway.OnCreate = FakeGateway.Processing;
            await service.PayAsync(order.Id, Card(payFull: true));

            var transaction = await db.PaymentTransactions.SingleAsync();
            transaction.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
            var mpOrderId = gateway.Orders.Keys.Single();
            gateway.Orders[mpOrderId] = FakeGateway.Approved(gateway.Created.Single()) with { Id = mpOrderId };

            Assert.Equal(1, await service.ReconcilePendingAsync());
            Assert.Equal(60m, (await db.Payments.SingleAsync()).AmountPaid);
            Assert.Equal(OrderStatus.PaymentConfirmed, (await db.Orders.FindAsync(order.Id))!.Status);
            Assert.Equal(0, await service.ReconcilePendingAsync());
        }

        [Fact]
        public async Task Reconcile_FindsByReference_WhenTheCreateResponseWasLost()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 40m);
            var (service, gateway, _) = Build(db);

            // Mercado Pago cobró, pero el servidor nunca recibió la respuesta (corte de red)
            var transaction = new PaymentTransaction
            {
                PaymentId = (await db.Payments.SingleAsync()).Id, OrderId = order.Id, MethodId = "visa", MethodType = "credit_card",
                Amount = 40m, Status = "error", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            };
            db.PaymentTransactions.Add(transaction);
            await db.SaveChangesAsync();
            gateway.Orders["ORD-LOST"] = new GatewayOrder("ORD-LOST", "processed", "accredited", transaction.Id.ToString("N"), 40m,
                [new GatewayPayment("PAY-9", "processed", "accredited", "visa", "credit_card")]);

            await service.ReconcilePendingAsync();

            Assert.Equal("ORD-LOST", (await db.PaymentTransactions.SingleAsync()).ExternalOrderId);
            Assert.Equal(40m, (await db.Payments.SingleAsync()).AmountPaid);
        }

        [Fact]
        public async Task CancelPending_OnlyVoidsOrdersWaitingForTheCustomer()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 60m);
            var (service, gateway, _) = Build(db);
            gateway.OnCreate = r => FakeGateway.Processing(r) with { Status = "action_required" };
            await service.PayAsync(order.Id, Card(payFull: true));

            await service.CancelPendingAsync(order.Id);

            Assert.Single(gateway.Cancelled);
            Assert.Equal("canceled", (await db.PaymentTransactions.SingleAsync()).Status);
        }

        [Fact]
        public async Task Report_SumsCollectedRefundedAndNet()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 100m);
            var (service, gateway, _) = Build(db);
            await service.PayAsync(order.Id, Card(payFull: false));
            gateway.OnCreate = FakeGateway.Failed;
            await service.PayAsync(order.Id, Card(payFull: true, token: "tok-2"));
            var approved = await db.PaymentTransactions.SingleAsync(t => t.ApprovedAt != null);
            await service.RefundAsync(order.Id, approved.Id, new RefundOnlinePaymentRequest { Amount = 10m });

            var report = await service.GetReportAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMinutes(1));

            Assert.Equal(50m, report.Collected);
            Assert.Equal(10m, report.Refunded);
            Assert.Equal(40m, report.Net);
            Assert.Equal((1, 1, 0), (report.ApprovedCount, report.RejectedCount, report.PendingCount));
            Assert.All(report.Rows, r => Assert.Equal("AN-1001", r.OrderCode));
        }

        [Fact]
        public async Task ResendingTheSameToken_DoesNotChargeOrCountTwice()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 100m);
            var (service, gateway, _) = Build(db);

            await service.PayAsync(order.Id, Card(payFull: false, token: "same-token"));

            // Mismo token = misma clave de idempotencia = misma order en Mercado Pago
            order.Status = OrderStatus.WaitingPayment;
            await db.SaveChangesAsync();
            await service.PayAsync(order.Id, Card(payFull: false, token: "same-token"));

            Assert.Equal(gateway.Created[0].IdempotencyKey, gateway.Created[1].IdempotencyKey);
            Assert.Single(db.PaymentTransactions);
            Assert.Equal(50m, (await db.Payments.SingleAsync()).AmountPaid);
        }

        [Fact]
        public async Task Yape_SendsYapeMethod_DniAndPhone()
        {
            using var db = GetDb();
            var order = await SeedOrder(db, total: 40m);
            var (service, gateway, _) = Build(db);

            var result = await service.PayAsync(order.Id, new CreateOnlinePaymentRequest
            {
                Method = "yape",
                Token = "yape-token",
                Phone = "111 111 111",
                PayFull = true
            });

            var sent = gateway.Created.Single();
            Assert.Equal("approved", result.Status);
            Assert.Equal("yape", sent.PaymentMethodId);
            Assert.Equal("debit_card", sent.PaymentMethodType);
            // Sin número de documento no se envía la identificación (la API lo exige si va)
            Assert.Null(sent.IdentificationType);
            Assert.Equal("111111111", sent.PhoneNumber);
            // Sin correo en el formulario se usa el del cliente del pedido
            Assert.Equal("maria@example.com", sent.PayerEmail);
        }

        [Theory]
        [InlineData(OrderStatus.WaitingQuote, "acepta la cotización")]
        [InlineData(OrderStatus.QuoteReady, "acepta la cotización")]
        [InlineData(OrderStatus.Cancelled, "cancelado")]
        public async Task OrdersThatCannotBePaid_AreRejectedBeforeCallingMercadoPago(OrderStatus status, string message)
        {
            using var db = GetDb();
            var order = await SeedOrder(db, status: status);
            var (service, gateway, _) = Build(db);

            var ex = await Assert.ThrowsAsync<OnlinePaymentException>(() => service.PayAsync(order.Id, Card(true)));

            Assert.Contains(message, ex.Message);
            Assert.Empty(gateway.Created);
        }

        [Fact]
        public async Task InvalidCardTypeOrYapePhone_AreRejected()
        {
            using var db = GetDb();
            var order = await SeedOrder(db);
            var (service, gateway, _) = Build(db);

            var card = Card(true);
            card.PaymentTypeId = "account_money";
            await Assert.ThrowsAsync<OnlinePaymentException>(() => service.PayAsync(order.Id, card));

            await Assert.ThrowsAsync<OnlinePaymentException>(() => service.PayAsync(order.Id,
                new CreateOnlinePaymentRequest { Method = "yape", Token = "t", Phone = "123" }));

            Assert.Empty(gateway.Created);
        }

        [Fact]
        public void WebhookSignature_UsesOfficialManifest()
        {
            const string secret = "test-secret";
            const string dataId = "ORD01ABCDEF";
            const string requestId = "req-123";
            const string ts = "1704908010";
            var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
            var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();

            WebhookSignatureValidator.Validate($"ts={ts},v1={hash}", requestId, dataId, secret);
            Assert.Throws<InvalidWebhookSignatureException>(() =>
                WebhookSignatureValidator.Validate($"ts={ts},v1={hash}", requestId, "ORD-OTRA", secret));
        }

        [Fact]
        public void RejectedOrderInErrorBody_IsParsed()
        {
            const string json = """
                {"id":"ORD01X","status":"failed","status_detail":"failed","external_reference":"abc","total_paid_amount":"0.00",
                 "transactions":{"payments":[{"id":"PAY01","status":"failed","status_detail":"insufficient_amount",
                 "payment_method":{"id":"visa","type":"credit_card"}}]}}
                """;

            var order = MercadoPagoGateway.TryParse(json)!;

            Assert.Equal("ORD01X", order.Id);
            Assert.True(order.IsFailed);
            Assert.Equal("insufficient_amount", order.Detail);
        }
    }
}
