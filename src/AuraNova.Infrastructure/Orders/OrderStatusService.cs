using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.OnlinePayments.Interfaces;
using AuraNova.Application.Orders;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Orders
{
    public class OrderStatusService : IOrderStatusService
    {
        private readonly AppDbContext _db;
        private readonly IOrderStatusTransitionService _transitions;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<OrderStatusService> _logger;
        private readonly IOnlinePaymentService? _onlinePayments;

        public OrderStatusService(
            AppDbContext db,
            IOrderStatusTransitionService transitions,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<OrderStatusService> logger,
            IOnlinePaymentService? onlinePayments = null)
        {
            _db = db;
            _transitions = transitions;
            _notificationService = notificationService;
            _currentUserService = currentUserService;
            _logger = logger;
            _onlinePayments = onlinePayments;
        }

        public async Task<OrderStatusChangeResponse> ChangeStatusAsync(Guid orderId, OrderStatus newStatus, string? comment)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
                throw new OrderNotFoundException($"Pedido con Id '{orderId}' no encontrado.");

            var oldStatus = order.Status;

            if (!_transitions.IsTransitionAllowed(oldStatus, newStatus, order.DeliveryType))
                throw new OrderValidationException(
                    $"Transición inválida: {oldStatus} → {newStatus} para tipo de entrega {order.DeliveryType}.");

            // Con solo el adelanto pagado, el pedido no sale hasta que se pague el saldo
            OrderBalance.EnsurePaidBeforeHandover(order, newStatus);

            // Sin precio no hay cotización: se usa "Enviar cotización", que guarda el total
            if (newStatus == OrderStatus.QuoteReady && order.Total is not > 0)
                throw new OrderValidationException(
                    "Para marcar la cotización como lista primero ingresa el precio en \"Enviar cotización\".");

            // Pasar a "esperando pago" requiere un total y un pago pendiente por ese monto
            if (newStatus == OrderStatus.WaitingPayment && order.Total is not > 0)
                throw new OrderValidationException("El pedido todavía no tiene un precio. Envía la cotización primero.");

            // Atomic: update order + create history
            var supportsTransactions = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            if (supportsTransactions)
                transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                order.Status = newStatus;
                order.UpdatedAt = DateTimeOffset.UtcNow;

                // El panel acepta la cotización en nombre del cliente (p. ej. lo confirmó por WhatsApp)
                if (oldStatus == OrderStatus.QuoteReady && newStatus == OrderStatus.WaitingPayment && order.Payment == null)
                {
                    _db.Payments.Add(new Payment { OrderId = order.Id, Amount = order.Total!.Value });
                }

                // Return stock if the order is cancelled (atomic increment, safe with concurrent orders)
                if (newStatus == OrderStatus.Cancelled && oldStatus != OrderStatus.Cancelled)
                {
                    foreach (var item in order.Items!)
                    {
                        await ProductStock.ReleaseAsync(_db, item.ProductId, item.Quantity);
                    }
                }

                var history = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = newStatus,
                    Comment = comment?.Trim(),
                    AdminUserId = _currentUserService.UserId,
                    AdminName = _currentUserService.Name
                };
                _db.Set<OrderStatusHistory>().Add(history);

                await _db.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                _logger.LogInformation("Pedido {OrderCode} cambió de {OldStatus} a {NewStatus}", order.OrderCode, oldStatus, newStatus);

                // Un pago que aún espera al cliente ya no debe poder completarse
                if (newStatus == OrderStatus.Cancelled && _onlinePayments != null)
                {
                    try
                    {
                        await _onlinePayments.CancelPendingAsync(order.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudieron anular los pagos pendientes del pedido {OrderCode}", order.OrderCode);
                    }
                }

                // --- Trigger Notification ---
                var notificationType = MapStatusToNotification(newStatus);
                if (notificationType.HasValue)
                {
                    await _notificationService.NotifyAsync(order.Id, notificationType.Value, comment);
                }

                return new OrderStatusChangeResponse
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Status = newStatus.ToString(),
                    StatusLabel = OrderStatusLabels.GetLabel(newStatus),
                    UpdatedAt = order.UpdatedAt.Value
                };
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        private NotificationType? MapStatusToNotification(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Preparing => NotificationType.OrderPreparing,
                OrderStatus.Ready => NotificationType.OrderReady,
                OrderStatus.Shipped => NotificationType.OrderShipped,
                OrderStatus.Delivered => NotificationType.OrderDelivered,
                OrderStatus.Cancelled => NotificationType.OrderCancelled,
                _ => null // Other statuses (like PaymentReported) are handled in their respective services
            };
        }

        public async Task<IReadOnlyList<OrderStatusHistoryResponse>> GetHistoryAsync(Guid orderId)
        {
            var order = await _db.Orders.AnyAsync(o => o.Id == orderId);
            if (!order)
                throw new OrderNotFoundException($"Pedido con Id '{orderId}' no encontrado.");

            var history = await _db.Set<OrderStatusHistory>()
                .Where(h => h.OrderId == orderId)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();

            return history.Select(h => new OrderStatusHistoryResponse
            {
                Status = h.Status.ToString(),
                Label = OrderStatusLabels.GetLabel(h.Status),
                Comment = h.Comment,
                CreatedAt = h.CreatedAt
            }).ToList();
        }
    }
}
