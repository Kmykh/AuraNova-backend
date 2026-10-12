using AuraNova.Application.Orders.Interfaces;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Orders
{
    public class OrderExpirationSettings
    {
        /// <summary>
        /// Horas sin pago tras las cuales un pedido de catálogo en WaitingPayment se cancela y libera su stock.
        /// 0 = desactivado (valor por defecto: es una política de negocio y debe activarse explícitamente).
        /// </summary>
        public int UnpaidExpirationHours { get; set; }

        public int CheckIntervalMinutes { get; set; } = 15;
    }

    /// <summary>
    /// En un live, pedidos que nunca se pagan dejan el stock bloqueado. Este servicio los cancela usando
    /// el mismo flujo que el panel (historial, devolución de stock y notificación).
    /// </summary>
    public class UnpaidOrderExpirationService(
        AppDbContext db,
        IOrderStatusService orderStatusService,
        IOptions<OrderExpirationSettings> options,
        ILogger<UnpaidOrderExpirationService> logger)
    {
        public async Task<int> ExpireAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            var hours = options.Value.UnpaidExpirationHours;
            if (hours <= 0)
                return 0;

            var cutoff = now.AddHours(-hours);

            // Solo pedidos de catálogo (los personalizados no reservan stock) sin evidencia de pago pendiente de revisión
            var candidates = await db.Orders
                .AsNoTracking()
                .Where(o => o.Status == OrderStatus.WaitingPayment && !o.IsCustomOrder)
                .Where(o => o.Payment == null || o.Payment.Status != PaymentStatus.Reported)
                .Select(o => new { o.Id, o.OrderCode, LastChange = o.UpdatedAt ?? o.CreatedAt })
                .ToListAsync(cancellationToken);

            var expired = 0;
            foreach (var order in candidates.Where(o => o.LastChange < cutoff))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await orderStatusService.ChangeStatusAsync(order.Id, OrderStatus.Cancelled,
                        $"Cancelado automáticamente: no se recibió el pago en {hours} h. El stock fue liberado.");
                    expired++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "No se pudo vencer el pedido impago {OrderCode}", order.OrderCode);
                }
            }

            if (expired > 0)
                logger.LogInformation("Pedidos impagos vencidos: {Count} (más de {Hours} h)", expired, hours);

            return expired;
        }
    }
}
