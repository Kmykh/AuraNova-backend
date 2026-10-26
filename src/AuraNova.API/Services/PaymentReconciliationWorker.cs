using AuraNova.Application.OnlinePayments.Interfaces;
using AuraNova.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace AuraNova.API.Services
{
    /// <summary>
    /// Respaldo de los webhooks de Mercado Pago: cada 5 minutos consulta los cobros que siguen sin resultado
    /// (en revisión, esperando al cliente o sin respuesta al crearlos) y actualiza el pedido.
    /// </summary>
    public class PaymentReconciliationWorker(
        IServiceScopeFactory scopes,
        IOptions<MercadoPagoSettings> settings,
        ILogger<PaymentReconciliationWorker> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(settings.Value.AccessToken))
                return;

            // Deja arrancar el servidor (y aplicar migraciones) antes de la primera vuelta
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IOnlinePaymentService>().ReconcilePendingAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error en la conciliación de pagos de Mercado Pago");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
