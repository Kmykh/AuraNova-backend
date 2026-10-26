using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>Envía un correo ya registrado como notificación y guarda si se envió o falló.</summary>
    public class EmailNotificationDispatcher(
        AppDbContext db,
        IEmailSender sender,
        IBusinessSettingsService businessSettings,
        ILogger<EmailNotificationDispatcher> logger)
    {
        public async Task DispatchAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            var notification = await db.Set<Notification>()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.Channel == NotificationChannel.Email, cancellationToken);
            if (notification == null || notification.Status == NotificationStatus.Sent)
                return;

            var businessName = (await businessSettings.GetPublicAsync()).BusinessName;
            if (string.IsNullOrWhiteSpace(businessName)) businessName = "Aura Nova";

            var subject = notification.Subject ?? businessName;
            var actionLabel = notification.ChannelUrl?.Contains("/pago/", StringComparison.Ordinal) == true ? "Ir a pagar" : "Ver mi pedido";
            var html = EmailTemplate.Render(businessName, subject, notification.Message, actionLabel, notification.ChannelUrl);

            try
            {
                // La clave cambia en cada reenvío manual (UpdatedAt) pero no en los reintentos automáticos
                var idempotencyKey = $"{notification.Id:N}-{(notification.UpdatedAt ?? notification.CreatedAt).ToUnixTimeMilliseconds()}";
                await sender.SendAsync(notification.Recipient, subject, html, notification.Message, idempotencyKey, cancellationToken);
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = DateTimeOffset.UtcNow;
                notification.ErrorMessage = null;
                logger.LogInformation("Correo {Type} enviado para el pedido {OrderId}", notification.Type, notification.OrderId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notification.Status = NotificationStatus.Failed;
                notification.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                logger.LogError(ex, "No se pudo enviar el correo {NotificationId}", notification.Id);
            }

            notification.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
