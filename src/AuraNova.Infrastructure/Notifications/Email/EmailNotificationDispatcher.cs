using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>Envía un correo ya registrado como notificación y guarda si se envió o falló.</summary>
    public class EmailNotificationDispatcher(
        AppDbContext db,
        IEmailSender sender,
        IBusinessSettingsService businessSettings,
        ILogger<EmailNotificationDispatcher> logger,
        IOptions<PublicSiteSettings>? siteOptions = null)
    {
        public async Task DispatchAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            var notification = await db.Set<Notification>()
                .Include(n => n.Order)
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.Channel == NotificationChannel.Email, cancellationToken);
            if (notification == null || notification.Status == NotificationStatus.Sent)
                return;

            var settings = await businessSettings.GetPublicAsync();
            var businessName = string.IsNullOrWhiteSpace(settings.BusinessName) ? "Aura Nova" : settings.BusinessName;
            var brand = new EmailBrand(businessName, siteOptions?.Value.BaseUrl ?? new PublicSiteSettings().BaseUrl, settings.WhatsAppNumber);

            var subject = notification.Subject ?? businessName;
            var layout = EmailCopy.For(notification.Type, notification.Subject, notification.ChannelUrl,
                notification.Order?.OrderCode ?? "", notification.Order?.IsCustomOrder == true);
            var html = EmailTemplate.Render(brand, layout, notification.Message);

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
