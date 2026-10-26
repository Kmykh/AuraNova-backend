using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Notifications.Email;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.API.Services
{
    /// <summary>Envía en segundo plano los correos de la cola (uno a la vez).</summary>
    public class EmailDispatchWorker(IEmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailDispatchWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RequeuePendingAsync(stoppingToken);

            await foreach (var notificationId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<EmailNotificationDispatcher>().DispatchAsync(notificationId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error enviando el correo {NotificationId}", notificationId);
                }
            }
        }

        /// <summary>Correos que no alcanzaron a salir antes de un reinicio del servidor (últimas 24 h).</summary>
        private async Task RequeuePendingAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var since = DateTimeOffset.UtcNow.AddHours(-24);
                var pending = await db.Set<Notification>()
                    .Where(n => n.Channel == NotificationChannel.Email && n.Status == NotificationStatus.Generated && n.CreatedAt > since)
                    .Select(n => n.Id)
                    .ToListAsync(cancellationToken);
                pending.ForEach(queue.Enqueue);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No se pudieron recuperar los correos pendientes");
            }
        }
    }
}
