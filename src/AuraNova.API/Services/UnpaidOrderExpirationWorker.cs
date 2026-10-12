using AuraNova.Infrastructure.Orders;
using Microsoft.Extensions.Options;

namespace AuraNova.API.Services
{
    /// <summary>
    /// Ejecuta periódicamente <see cref="UnpaidOrderExpirationService"/>. No hace nada si
    /// Orders:UnpaidExpirationHours es 0.
    /// </summary>
    public class UnpaidOrderExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OrderExpirationSettings> options,
        ILogger<UnpaidOrderExpirationWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (options.Value.UnpaidExpirationHours <= 0)
            {
                logger.LogInformation("Vencimiento de pedidos impagos desactivado (Orders:UnpaidExpirationHours = 0).");
                return;
            }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.CheckIntervalMinutes)));
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<UnpaidOrderExpirationService>();
                    await service.ExpireAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error al vencer pedidos impagos.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
