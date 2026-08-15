using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Payments.DTOs;
using AuraNova.Application.Payments.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _db;
        private readonly IFileStorageService _storageService;
        private readonly INotificationService _notificationService;
        private readonly PaymentSettings _settings;
        private readonly ILogger<PaymentService> _logger;

        private const int MaxFileSizeMB = 5;
        private const long MaxFileSizeBytes = MaxFileSizeMB * 1024 * 1024;
        private readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/webp" };

        public PaymentService(
            AppDbContext db,
            IFileStorageService storageService,
            INotificationService notificationService,
            IOptions<PaymentSettings> options,
            ILogger<PaymentService> logger)
        {
            _db = db;
            _storageService = storageService;
            _notificationService = notificationService;
            _settings = options.Value;
            _logger = logger;
        }

        public PaymentInfoResponse GetPaymentInfo()
        {
            return new PaymentInfoResponse
            {
                Enabled = _settings.YapeEnabled,
                Method = PaymentMethod.Yape.ToString(),
                HolderName = _settings.YapeHolderName,
                QrImageUrl = _settings.YapeQrImageUrl
            };
        }

        public async Task<PaymentResponse> ReportEvidenceAsync(Guid orderId, Stream fileStream, string fileName, string contentType)
        {
            var order = await _db.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                throw new OrderNotFoundException($"Pedido con Id '{orderId}' no encontrado.");

            if (order.Status != OrderStatus.WaitingPayment)
                throw new OrderValidationException($"El pedido '{order.OrderCode}' no está esperando pago. Estado actual: {order.Status}");

            var payment = order.Payment;
            if (payment == null)
                throw new OrderValidationException($"El pedido '{order.OrderCode}' no tiene un pago generado.");

            if (payment.Status == PaymentStatus.Confirmed)
                throw new OrderValidationException($"El pago del pedido '{order.OrderCode}' ya está confirmado.");

            // File validations
            if (fileStream.Length == 0 || fileStream.Length > MaxFileSizeBytes)
                throw new OrderValidationException($"El archivo debe ser mayor a 0 y menor a {MaxFileSizeMB} MB.");

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext) || !AllowedMimeTypes.Contains(contentType.ToLowerInvariant()))
                throw new OrderValidationException($"Formato de archivo no permitido. Solo se aceptan: {string.Join(", ", AllowedExtensions)}.");

            var folderPath = $"payment-evidence/{order.Id}";
            var uploadedPath = await _storageService.UploadAsync(fileStream, fileName, contentType, folderPath);

            try
            {
                payment.EvidenceUrl = uploadedPath;
                payment.Status = PaymentStatus.Reported;
                payment.UpdatedAt = DateTimeOffset.UtcNow;
                
                order.Status = OrderStatus.PaymentReported;
                order.UpdatedAt = DateTimeOffset.UtcNow;

                _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = OrderStatus.PaymentReported,
                    Comment = "Cliente subió evidencia de pago."
                });

                await _db.SaveChangesAsync();

                _logger.LogInformation("Evidencia reportada para pedido {OrderCode}. PaymentId: {PaymentId}", order.OrderCode, payment.Id);

                // --- Trigger Notification ---
                await _notificationService.NotifyAsync(order.Id, NotificationType.PaymentReported);

                return MapToResponse(payment, order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando la evidencia en DB. Intentando borrar de storage: {Path}", uploadedPath);
                // Attempt to cleanup storage if DB fails
                await _storageService.DeleteAsync(uploadedPath);
                throw;
            }
        }

        public async Task<IReadOnlyList<AdminPaymentResponse>> GetAdminPaymentsAsync()
        {
            var payments = await _db.Payments
                .Include(p => p.Order!)
                    .ThenInclude(o => o.Customer)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return payments.Select(MapToAdminResponse).ToList();
        }

        public async Task<AdminPaymentResponse?> GetAdminPaymentByIdAsync(Guid paymentId)
        {
            var payment = await _db.Payments
                .Include(p => p.Order!)
                    .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            return payment == null ? null : MapToAdminResponse(payment);
        }

        public async Task<bool> ConfirmAsync(Guid paymentId)
        {
            var payment = await _db.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null) return false;

            if (payment.Status == PaymentStatus.Confirmed)
                throw new OrderValidationException("El pago ya ha sido confirmado previamente.");

            if (payment.Status != PaymentStatus.Reported)
                throw new OrderValidationException("El pago debe tener evidencia reportada para ser confirmado.");

            payment.Status = PaymentStatus.Confirmed;
            payment.VerifiedAt = DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;

            payment.Order!.Status = OrderStatus.PaymentConfirmed;
            payment.Order.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = payment.OrderId,
                Status = OrderStatus.PaymentConfirmed,
                Comment = "Pago confirmado por administrador."
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation("Pago {PaymentId} confirmado para pedido {OrderCode}.", payment.Id, payment.Order.OrderCode);

            // --- Trigger Notification ---
            await _notificationService.NotifyAsync(payment.OrderId, NotificationType.PaymentConfirmed);

            return true;
        }

        public async Task<bool> RejectAsync(Guid paymentId, RejectPaymentRequest request)
        {
            var payment = await _db.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null) return false;

            if (payment.Status == PaymentStatus.Confirmed)
                throw new OrderValidationException("El pago ya fue confirmado, no se puede rechazar directamente.");

            payment.Status = PaymentStatus.Rejected;
            payment.Notes = request.Notes.Trim();
            payment.VerifiedAt = DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;

            payment.Order!.Status = OrderStatus.WaitingPayment; // Regresa a WaitingPayment
            payment.Order.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = payment.OrderId,
                Status = OrderStatus.WaitingPayment,
                Comment = $"La evidencia de pago fue rechazada. Motivo: {request.Notes.Trim()}"
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation("Pago {PaymentId} rechazado para pedido {OrderCode}. Motivo: {Notes}", payment.Id, payment.Order.OrderCode, request.Notes);

            // --- Trigger Notification ---
            await _notificationService.NotifyAsync(payment.OrderId, NotificationType.PaymentRejected, request.Notes.Trim());

            return true;
        }

        private static PaymentResponse MapToResponse(Payment payment, Order order)
        {
            return new PaymentResponse
            {
                Id = payment.Id,
                OrderId = payment.OrderId,
                OrderCode = order.OrderCode,
                Method = payment.Method.ToString(),
                Amount = payment.Amount,
                Status = payment.Status.ToString(),
                EvidenceUrl = payment.EvidenceUrl,
                Notes = payment.Notes,
                CreatedAt = payment.CreatedAt,
                UpdatedAt = payment.UpdatedAt,
                VerifiedAt = payment.VerifiedAt
            };
        }

        private static AdminPaymentResponse MapToAdminResponse(Payment payment)
        {
            return new AdminPaymentResponse
            {
                Id = payment.Id,
                OrderId = payment.OrderId,
                OrderCode = payment.Order!.OrderCode,
                Method = payment.Method.ToString(),
                Amount = payment.Amount,
                Status = payment.Status.ToString(),
                EvidenceUrl = payment.EvidenceUrl,
                Notes = payment.Notes,
                CreatedAt = payment.CreatedAt,
                UpdatedAt = payment.UpdatedAt,
                VerifiedAt = payment.VerifiedAt,
                CustomerName = payment.Order.Customer?.Name ?? "",
                CustomerPhone = payment.Order.Customer?.Phone ?? ""
            };
        }
    }
}
