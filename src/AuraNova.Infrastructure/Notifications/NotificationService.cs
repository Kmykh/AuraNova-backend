
using AuraNova.Application.Notifications.DTOs;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.WhatsApp.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using AuraNova.Infrastructure.Notifications.Email;

namespace AuraNova.Infrastructure.Notifications
{
    public class NotificationService(
        AppDbContext db,
        INotificationTemplateService templateService,
        IWhatsAppMessageService whatsAppService,
        ILogger<NotificationService> logger,
        IConfiguration configuration,
        IEmailQueue? emailQueue = null,
        IOptions<EmailSettings>? emailOptions = null,
        IOptions<PublicSiteSettings>? siteOptions = null) : INotificationService
    {
        private readonly string _siteUrl = (siteOptions?.Value.BaseUrl ?? new PublicSiteSettings().BaseUrl).TrimEnd('/');
        private readonly AppDbContext _db = db;
        private readonly INotificationTemplateService _templateService = templateService;
        private readonly IWhatsAppMessageService _whatsAppService = whatsAppService;
        private readonly ILogger<NotificationService> _logger = logger;
        private readonly IConfiguration _configuration = configuration;


        public async Task NotifyAsync(Guid orderId, NotificationType type, string? reason = null)
        {
            var enabledStr = _configuration["NotificationSettings:Enabled"];
            if (bool.TryParse(enabledStr, out var enabled) && !enabled)
            {
                _logger.LogInformation("Notificaciones están desactivadas. Omitiendo notificación {Type} para pedido {OrderId}.", type, orderId);
                return;
            }

            try
            {
                var order = await _db.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.Quote)
                    .Include(o => o.Payment)
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

                await QueueEmailAsync(order, type, message);
            }
            catch (Exception ex)
            {
                // We DO NOT rethrow. The main business transaction has already succeeded.
                _logger.LogError(ex, "Failed to generate notification {Type} for order {OrderId}", type, orderId);
            }
        }

        /// <summary>El mismo mensaje por correo, enviado automáticamente en segundo plano.</summary>
        private async Task QueueEmailAsync(Order order, NotificationType type, string message)
        {
            if (emailQueue == null || emailOptions?.Value.IsConfigured != true)
                return;

            var email = order.Customer?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _))
                return;

            var notification = new Notification
            {
                OrderId = order.Id,
                Type = type,
                Channel = NotificationChannel.Email,
                Status = NotificationStatus.Generated,
                Recipient = email,
                Subject = EmailSubject(type, order),
                Message = message,
                ChannelUrl = EmailActionUrl(type, order)
            };
            _db.Set<Notification>().Add(notification);
            await _db.SaveChangesAsync();

            emailQueue.Enqueue(notification.Id);
        }

        private static decimal BalanceDue(Order order) =>
            order.Payment == null ? 0m : Math.Max(0m, order.Payment.Amount - order.Payment.AmountPaid);

        /// <summary>El botón del correo lleva a pagar cuando hay algo pendiente; si no, al seguimiento.</summary>
        private string EmailActionUrl(NotificationType type, Order order)
        {
            var needsPayment = type switch
            {
                NotificationType.QuoteReady or NotificationType.PaymentRejected => true,
                NotificationType.OrderCreated => order.Status == OrderStatus.WaitingPayment,
                NotificationType.PaymentConfirmed or NotificationType.OrderReady => BalanceDue(order) > 0,
                _ => false
            };
            return needsPayment ? $"{_siteUrl}/pago/{order.Id}" : $"{_siteUrl}/seguimiento/{order.TrackingToken}";
        }

        public static string EmailSubject(NotificationType type, Order order) => type switch
        {
            NotificationType.OrderCreated => order.IsCustomOrder
                ? $"Recibimos tu pedido personalizado {order.OrderCode}"
                : $"Tu pedido {order.OrderCode} está registrado",
            NotificationType.QuoteReady => $"Tu cotización del pedido {order.OrderCode} está lista",
            NotificationType.PaymentReported => $"Recibimos tu comprobante · {order.OrderCode}",
            NotificationType.PaymentConfirmed => $"¡Pago confirmado! Pedido {order.OrderCode}",
            NotificationType.BalancePaid => $"¡Pedido pagado por completo! {order.OrderCode}",
            NotificationType.PaymentRejected => $"No pudimos validar tu pago · {order.OrderCode}",
            NotificationType.OrderPreparing => $"Tu pedido {order.OrderCode} está en el taller",
            NotificationType.OrderReady => BalanceDue(order) > 0
                ? $"Tu pedido {order.OrderCode} está listo: falta el saldo"
                : $"Tu pedido {order.OrderCode} está listo",
            NotificationType.OrderShipped => $"Tu pedido {order.OrderCode} va en camino",
            NotificationType.OrderDelivered => $"¡Tu pedido {order.OrderCode} fue entregado!",
            NotificationType.OrderCancelled => $"Tu pedido {order.OrderCode} fue cancelado",
            _ => $"Novedades de tu pedido {order.OrderCode}"
        };

        public async Task<bool> ResendEmailAsync(Guid notificationId)
        {
            var notification = await _db.Set<Notification>().FirstOrDefaultAsync(n => n.Id == notificationId);
            if (notification == null || notification.Channel != NotificationChannel.Email || emailQueue == null)
                return false;

            notification.Status = NotificationStatus.Generated;
            notification.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            emailQueue.Enqueue(notification.Id);
            return true;
        }

        public async Task<IReadOnlyList<NotificationResponse>> GetPendingWhatsAppAsync()
        {
            var since = DateTimeOffset.UtcNow.AddDays(-14);
            return await _db.Set<Notification>()
                .Where(n => n.Channel == NotificationChannel.WhatsApp && n.Status == NotificationStatus.Generated && n.CreatedAt > since)
                .OrderBy(n => n.CreatedAt)
                .Select(n => new NotificationResponse
                {
                    Id = n.Id,
                    OrderId = n.OrderId,
                    OrderCode = n.Order!.OrderCode,
                    CustomerName = n.Order.Customer != null ? n.Order.Customer.Name : null,
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
        }

        private async Task<string> BuildMessageAsync(NotificationType type, Order order, string? reason)
        {
            return type switch
            {
                NotificationType.OrderCreated => await _templateService.BuildOrderCreatedMessageAsync(order),
                NotificationType.QuoteReady => await _templateService.BuildQuoteReadyMessageAsync(order),
                NotificationType.PaymentReported => await _templateService.BuildPaymentReportedMessageAsync(order),
                NotificationType.PaymentConfirmed => await _templateService.BuildPaymentConfirmedMessageAsync(order),
                NotificationType.BalancePaid => await _templateService.BuildBalancePaidMessageAsync(order),
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
                    Subject = n.Subject,
                    Message = n.Message,
                    ChannelUrl = n.ChannelUrl,
                    ErrorMessage = n.ErrorMessage,
                    SentAt = n.SentAt,
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
