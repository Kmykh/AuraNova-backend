using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Notifications.DTOs;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.WhatsApp.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Notifications
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly INotificationTemplateService _templateService;
        private readonly IWhatsAppMessageService _whatsAppService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext db,
            INotificationTemplateService templateService,
            IWhatsAppMessageService whatsAppService,
            ILogger<NotificationService> logger)
        {
            _db = db;
            _templateService = templateService;
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        public async Task NotifyAsync(Guid orderId, NotificationType type, string? reason = null)
        {
            try
            {
                var order = await _db.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.Quote)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || order.Customer == null)
                {
                    _logger.LogWarning("Notification failed: Order {OrderId} or Customer not found.", orderId);
                    return;
                }

                // Protect against duplicates for certain status changes within a very short timeframe or simply if the exact notification already exists
                var existing = await _db.Set<Notification>()
                    .Where(n => n.OrderId == orderId && n.Type == type && n.Status != NotificationStatus.Failed)
                    .OrderByDescending(n => n.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existing != null && (DateTimeOffset.UtcNow - existing.CreatedAt).TotalMinutes < 5)
                {
                    _logger.LogInformation("Skipping duplicate notification {Type} for order {OrderId}", type, orderId);
                    return;
                }

                var phone = order.Customer.Phone;
                if (string.IsNullOrWhiteSpace(phone))
                {
                    _logger.LogWarning("Notification failed: Customer {CustomerId} has no phone.", order.CustomerId);
                    return;
                }

                var normalizedPhone = _whatsAppService.NormalizePhone(phone);
                var message = await BuildMessageAsync(type, order, reason);
                var url = await _whatsAppService.GenerateUrlAsync(phone, message);

                var notification = new Notification
                {
                    OrderId = order.Id,
                    Type = type,
                    Channel = NotificationChannel.WhatsApp,
                    Status = NotificationStatus.Generated,
                    Recipient = normalizedPhone,
                    Message = message,
                    ChannelUrl = url
                };

                _db.Set<Notification>().Add(notification);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Notification {Type} generated for order {OrderId}", type, orderId);
            }
            catch (Exception ex)
            {
                // We DO NOT rethrow. The main business transaction has already succeeded.
                _logger.LogError(ex, "Failed to generate notification {Type} for order {OrderId}", type, orderId);
            }
        }

        private async Task<string> BuildMessageAsync(NotificationType type, Order order, string? reason)
        {
            return type switch
            {
                NotificationType.OrderCreated => await _templateService.BuildOrderCreatedMessageAsync(order),
                NotificationType.QuoteReady => await _templateService.BuildQuoteReadyMessageAsync(order),
                NotificationType.PaymentReported => await _templateService.BuildPaymentReportedMessageAsync(order),
                NotificationType.PaymentConfirmed => await _templateService.BuildPaymentConfirmedMessageAsync(order),
                NotificationType.PaymentRejected => await _templateService.BuildPaymentRejectedMessageAsync(order, reason),
                NotificationType.OrderPreparing => await _templateService.BuildPreparingMessageAsync(order),
                NotificationType.OrderReady => await _templateService.BuildReadyMessageAsync(order),
                NotificationType.OrderShipped => await _templateService.BuildShippedMessageAsync(order),
                NotificationType.OrderDelivered => await _templateService.BuildDeliveredMessageAsync(order),
                NotificationType.OrderCancelled => await _templateService.BuildCancelledMessageAsync(order, reason),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public async Task<IReadOnlyList<NotificationResponse>> GetByOrderIdAsync(Guid orderId)
        {
            var notifications = await _db.Set<Notification>()
                .Where(n => n.OrderId == orderId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationResponse
                {
                    Id = n.Id,
                    OrderId = n.OrderId,
                    OrderCode = n.Order!.OrderCode,
                    Type = n.Type.ToString(),
                    Channel = n.Channel.ToString(),
                    Status = n.Status.ToString(),
                    Recipient = n.Recipient,
                    Message = n.Message,
                    ChannelUrl = n.ChannelUrl,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt
                })
                .ToListAsync();

            return notifications;
        }

        public async Task<NotificationResponse?> GetByIdAsync(Guid id)
        {
            var notification = await _db.Set<Notification>()
                .Include(n => n.Order)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
                return null;

            return new NotificationResponse
            {
                Id = notification.Id,
                OrderId = notification.OrderId,
                OrderCode = notification.Order!.OrderCode,
                Type = notification.Type.ToString(),
                Channel = notification.Channel.ToString(),
                Status = notification.Status.ToString(),
                Recipient = notification.Recipient,
                Message = notification.Message,
                ChannelUrl = notification.ChannelUrl,
                CreatedAt = notification.CreatedAt,
                UpdatedAt = notification.UpdatedAt
            };
        }

        public async Task<WhatsAppPreparationResponse?> PrepareWhatsAppAsync(Guid id)
        {
            var notification = await _db.Set<Notification>().FirstOrDefaultAsync(n => n.Id == id);
            if (notification == null) return null;

            if (notification.Status == NotificationStatus.Generated)
            {
                notification.Status = NotificationStatus.Opened;
                notification.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
            }

            return new WhatsAppPreparationResponse
            {
                NotificationId = notification.Id,
                Status = notification.Status.ToString(),
                Phone = notification.Recipient,
                Message = notification.Message,
                WhatsappUrl = notification.ChannelUrl ?? ""
            };
        }
    }
}
