using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Notifications.Interfaces;
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
        private readonly ILogger<OrderStatusService> _logger;

        public OrderStatusService(
            AppDbContext db,
            IOrderStatusTransitionService transitions,
            INotificationService notificationService,
            ILogger<OrderStatusService> logger)
        {
            _db = db;
            _transitions = transitions;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<OrderStatusChangeResponse> ChangeStatusAsync(Guid orderId, OrderStatus newStatus, string? comment)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
                throw new OrderNotFoundException($"Pedido con Id '{orderId}' no encontrado.");

            var oldStatus = order.Status;

            if (!_transitions.IsTransitionAllowed(oldStatus, newStatus, order.DeliveryType))
                throw new OrderValidationException(
                    $"Transición inválida: {oldStatus} → {newStatus} para tipo de entrega {order.DeliveryType}.");

            // Atomic: update order + create history
            var supportsTransactions = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            if (supportsTransactions)
                transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                order.Status = newStatus;
                order.UpdatedAt = DateTimeOffset.UtcNow;

                var history = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = newStatus,
                    Comment = comment?.Trim()
                };
                _db.Set<OrderStatusHistory>().Add(history);

                await _db.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                _logger.LogInformation("Pedido {OrderCode} cambió de {OldStatus} a {NewStatus}", order.OrderCode, oldStatus, newStatus);

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
