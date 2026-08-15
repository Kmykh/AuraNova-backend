using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Payments.DTOs;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Payments;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class PaymentServiceTests
    {
        private AppDbContext GetInMemoryDb(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        private class FakeStorageService : IFileStorageService
        {
            public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder)
            {
                return Task.FromResult($"{folder}/fake-uuid-1234.jpg");
            }

            public Task DeleteAsync(string path)
            {
                return Task.CompletedTask;
            }
        }

        private PaymentService CreateService(AppDbContext db)
        {
            var options = Options.Create(new PaymentSettings { YapeEnabled = true, YapeHolderName = "Test" });
            var notificationService = new Mock<INotificationService>();
            return new PaymentService(db, new FakeStorageService(), notificationService.Object, options, new NullLogger<PaymentService>());
        }

        private async Task<(Order order, Payment payment)> SeedWaitingPaymentOrder(AppDbContext db)
        {
            var order = new Order
            {
                OrderCode = "PED-2026-000001",
                Status = OrderStatus.WaitingPayment,
                Total = 100m
            };
            db.Orders.Add(order);

            var payment = new Payment
            {
                OrderId = order.Id,
                Status = PaymentStatus.Pending,
                Amount = 100m,
                Method = PaymentMethod.Yape
            };
            db.Payments.Add(payment);

            await db.SaveChangesAsync();
            return (order, payment);
        }

        [Fact]
        public async Task ReportEvidenceAsync_ShouldUpdateStatusesAndEvidenceUrl()
        {
            using var db = GetInMemoryDb(Guid.NewGuid().ToString());
            var (order, payment) = await SeedWaitingPaymentOrder(db);
            var service = CreateService(db);

            var fileContent = "fake image content";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));

            var result = await service.ReportEvidenceAsync(order.Id, stream, "evidence.jpg", "image/jpeg");

            Assert.Equal("Reported", result.Status);
            Assert.NotNull(result.EvidenceUrl);
            Assert.Contains("fake-uuid", result.EvidenceUrl);

            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.Equal(OrderStatus.PaymentReported, dbOrder!.Status);
            
            var dbPayment = await db.Payments.FindAsync(payment.Id);
            Assert.Equal(PaymentStatus.Reported, dbPayment!.Status);
        }

        [Fact]
        public async Task ReportEvidenceAsync_ShouldRejectInvalidExtension()
        {
            using var db = GetInMemoryDb(Guid.NewGuid().ToString());
            var (order, _) = await SeedWaitingPaymentOrder(db);
            var service = CreateService(db);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf content"));

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                service.ReportEvidenceAsync(order.Id, stream, "document.pdf", "application/pdf"));

            Assert.Contains("Formato de archivo no permitido", ex.Message);
        }

        [Fact]
        public async Task ReportEvidenceAsync_ShouldRejectLargeFile()
        {
            using var db = GetInMemoryDb(Guid.NewGuid().ToString());
            var (order, _) = await SeedWaitingPaymentOrder(db);
            var service = CreateService(db);

            // Create a fake stream that reports a large length but doesn't actually allocate memory
            var stream = new MockLargeStream(6 * 1024 * 1024); // 6MB

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() =>
                service.ReportEvidenceAsync(order.Id, stream, "large.jpg", "image/jpeg"));

            Assert.Contains("menor a 5 MB", ex.Message);
        }

        [Fact]
        public async Task ConfirmAsync_ShouldUpdateToConfirmed()
        {
            using var db = GetInMemoryDb(Guid.NewGuid().ToString());
            var (order, payment) = await SeedWaitingPaymentOrder(db);
            payment.Status = PaymentStatus.Reported;
            await db.SaveChangesAsync();

            var service = CreateService(db);

            var result = await service.ConfirmAsync(payment.Id);

            Assert.True(result);
            
            var dbPayment = await db.Payments.FindAsync(payment.Id);
            Assert.Equal(PaymentStatus.Confirmed, dbPayment!.Status);
            Assert.NotNull(dbPayment.VerifiedAt);

            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.Equal(OrderStatus.PaymentConfirmed, dbOrder!.Status);
        }

        [Fact]
        public async Task ConfirmAsync_ShouldFailIfAlreadyConfirmed()
        {
            using var db = GetInMemoryDb(Guid.NewGuid().ToString());
            var (order, payment) = await SeedWaitingPaymentOrder(db);
            payment.Status = PaymentStatus.Confirmed;
            await db.SaveChangesAsync();

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<OrderValidationException>(() => service.ConfirmAsync(payment.Id));
            Assert.Contains("ya ha sido confirmado", ex.Message);
        }

        [Fact]
        public async Task RejectAsync_ShouldUpdateToRejectedAndResetOrder()
        {
            using var db = GetInMemoryDb(Guid.NewGuid().ToString());
            var (order, payment) = await SeedWaitingPaymentOrder(db);
            payment.Status = PaymentStatus.Reported;
            order.Status = OrderStatus.PaymentReported;
            await db.SaveChangesAsync();

            var service = CreateService(db);

            var request = new RejectPaymentRequest { Notes = "Monto incorrecto" };
            var result = await service.RejectAsync(payment.Id, request);

            Assert.True(result);
            
            var dbPayment = await db.Payments.FindAsync(payment.Id);
            Assert.Equal(PaymentStatus.Rejected, dbPayment!.Status);
            Assert.Equal("Monto incorrecto", dbPayment.Notes);

            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.Equal(OrderStatus.WaitingPayment, dbOrder!.Status); // Order goes back to WaitingPayment
        }

        // Helper class to mock stream length without allocating memory
        private class MockLargeStream : MemoryStream
        {
            private readonly long _length;
            public MockLargeStream(long length) { _length = length; }
            public override long Length => _length;
        }
    }
}
