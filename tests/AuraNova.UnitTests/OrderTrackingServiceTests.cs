using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraNova.UnitTests
{
    public class OrderTrackingServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private async Task<Order> SeedOrderWithHistory(AppDbContext db)
        {
            var customer = new Customer { Name = "María", Phone = "999999999", Email = "maria@test.com" };
            db.Customers.Add(customer);

            var order = new Order
            {
                CustomerId = customer.Id,
                OrderCode = "PED-2026-000001",
                Status = OrderStatus.Preparing,
                DeliveryType = DeliveryType.Delivery,
                Subtotal = 80m,
                DeliveryCost = 7m,
                Total = 87m
            };
            db.Orders.Add(order);

            // Seed history
            db.OrderStatusHistory.AddRange(
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.WaitingPayment },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PaymentReported },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PaymentConfirmed },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Preparing }
            );

            await db.SaveChangesAsync();
            return order;
        }

        // =====================================================================
        // DINERO DEL CLIENTE: pagado, devuelto o por devolver
        // =====================================================================

        private static async Task<Order> SeedPaidOrder(AppDbContext db, OrderStatus status, params PaymentTransaction[] transactions)
        {
            var customer = new Customer { Name = "Lucía Torres", Phone = "988777666" };
            db.Customers.Add(customer);
            var order = new Order { CustomerId = customer.Id, OrderCode = "AN-1040", Status = status, DeliveryType = DeliveryType.Delivery, Subtotal = 80m, DeliveryCost = 7m, Total = 87m };
            db.Orders.Add(order);
            var payment = new Payment { OrderId = order.Id, Amount = 87m, Method = PaymentMethod.MercadoPago, Status = PaymentStatus.Confirmed };
            db.Payments.Add(payment);
            foreach (var t in transactions)
            {
                t.PaymentId = payment.Id;
                t.OrderId = order.Id;
                db.PaymentTransactions.Add(t);
            }
            payment.AmountPaid = transactions.Where(t => t.ApprovedAt != null).Sum(t => t.Amount - t.RefundedAmount);
            await db.SaveChangesAsync();
            return order;
        }

        [Fact]
        public async Task Tracking_CancelledAndRefunded_ExplainsTheRefundWithoutTeamNotes()
        {
            using var db = GetInMemoryDb();
            var paidAt = DateTimeOffset.UtcNow.AddDays(-2);
            var order = await SeedPaidOrder(db, OrderStatus.Cancelled, new PaymentTransaction
            {
                MethodId = "yape", Amount = 43.50m, IsDeposit = true, Status = "refunded", ApprovedAt = paidAt,
                RefundedAmount = 43.50m, RefundedAt = paidAt.AddDays(1)
            });
            db.OrderStatusHistory.AddRange(
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.WaitingPayment, CreatedAt = paidAt.AddMinutes(-5) },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PaymentConfirmed, CreatedAt = paidAt, Comment = "Adelanto pagado con Yape (Mercado Pago): S/ 43.50. Saldo pendiente: S/ 43.50." },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Cancelled, CreatedAt = paidAt.AddHours(20), Comment = "Cancelado: Nos quedamos sin girasoles" },
                new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Cancelled, CreatedAt = paidAt.AddDays(1), Comment = "Devolución de S/ 43.50 (Yape) por Mercado Pago. Motivo: pedido cancelado" });
            await db.SaveChangesAsync();

            var result = await new OrderTrackingService(db, new TestStorage()).GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.Equal("refunded", result!.Payment.State);
            Assert.Equal(43.50m, result.Payment.Refunded);
            Assert.Equal(0m, result.Payment.NetPaid);
            Assert.Equal(["payment", "refund"], result.Payment.Movements.Select(m => m.Kind));
            Assert.All(result.Payment.Movements, m => Assert.Equal("Yape", m.Method));

            Assert.DoesNotContain(result.Timeline, i => i.Description?.StartsWith("Devolución") == true);
            Assert.Equal("Adelanto pagado", result.Timeline.Single(i => i.Kind == "payment").Label);
            Assert.Equal("Motivo: Nos quedamos sin girasoles", result.Timeline.Single(i => i.Kind == "cancelled").Description);
            var refund = result.Timeline.Last();
            Assert.Equal("refund", refund.Kind);
            Assert.Equal("S/ 43.50 a tu Yape.", refund.Description);
        }

        [Fact]
        public async Task Tracking_CancelledWithMoneyStillPaid_IsPendingRefund()
        {
            using var db = GetInMemoryDb();
            var order = await SeedPaidOrder(db, OrderStatus.Cancelled, new PaymentTransaction
            {
                MethodId = "debvisa", Amount = 87m, Status = "processed", ApprovedAt = DateTimeOffset.UtcNow.AddHours(-1)
            });
            db.OrderStatusHistory.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Cancelled, Comment = "Se aprobó un pago de S/ 87.00 con el pedido cancelado. Devuélvelo desde Pagos." });
            await db.SaveChangesAsync();

            var result = await new OrderTrackingService(db, new TestStorage()).GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.Equal("refund_pending", result!.Payment.State);
            Assert.Equal(87m, result.Payment.NetPaid);
            Assert.Equal(0m, result.Payment.BalanceDue);
            Assert.Equal("Visa", result.Payment.Movements.Single().Method);
            Assert.DoesNotContain(result.Timeline, i => i.Description?.Contains("Devuélvelo") == true);
        }

        [Theory]
        [InlineData(43.50, "deposit", 43.50)]
        [InlineData(87, "paid", 0)]
        public async Task Tracking_ActiveOrder_ShowsWhatWasPaidAndWhatIsMissing(double paid, string state, double due)
        {
            using var db = GetInMemoryDb();
            var order = await SeedPaidOrder(db, OrderStatus.Preparing, new PaymentTransaction
            {
                MethodId = "master", Amount = (decimal)paid, IsDeposit = paid < 87, Status = "processed", ApprovedAt = DateTimeOffset.UtcNow
            });

            var result = await new OrderTrackingService(db, new TestStorage()).GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.Equal(state, result!.Payment.State);
            Assert.Equal((decimal)due, result.Payment.BalanceDue);
            Assert.Equal("Mastercard", result.Payment.Movements.Single().Method);
        }

        [Fact]
        public async Task GetTrackingAsync_CorrectToken_ShouldReturnTracking()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db, new TestStorage());

            var result = await service.GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.NotNull(result);
            Assert.Equal("PED-2026-000001", result!.OrderCode);
            Assert.Equal("Preparing", result.Status);
            Assert.Equal("Preparando tu pedido", result.StatusLabel);
            Assert.Equal("Delivery", result.DeliveryType);
            Assert.Equal(87m, result.Costs.Total);
        }

        [Fact]
        public async Task GetTrackingAsync_CorrectToken_ShouldReturnTimeline()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db, new TestStorage());

            var result = await service.GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.NotNull(result);
            Assert.Equal(4, result!.Timeline.Count);
            Assert.Equal("WaitingPayment", result.Timeline[0].Status);
            Assert.Equal("Preparing", result.Timeline[3].Status);
            Assert.True(result.Timeline[0].Completed);
        }

        [Fact]
        public async Task GetTrackingAsync_WrongToken_ShouldReturnNull()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db, new TestStorage());

            var result = await service.GetTrackingAsync(order.OrderCode, "wrong-token");
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTrackingAsync_WrongOrderCode_ShouldReturnNull()
        {
            using var db = GetInMemoryDb();
            await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db, new TestStorage());

            var result = await service.GetTrackingAsync("PED-FAKE-999999", "any-token");
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTrackingAsync_ShouldNotExposePhone()
        {
            using var db = GetInMemoryDb();
            var order = await SeedOrderWithHistory(db);
            var service = new OrderTrackingService(db, new TestStorage());

            var result = await service.GetTrackingAsync(order.OrderCode, order.TrackingToken);

            Assert.NotNull(result);
            // PublicTrackingResponse has no phone/email/token properties
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.DoesNotContain("999999999", json);
            Assert.DoesNotContain("maria@test.com", json);
            Assert.DoesNotContain(order.TrackingToken, json);
        }
    }
}
