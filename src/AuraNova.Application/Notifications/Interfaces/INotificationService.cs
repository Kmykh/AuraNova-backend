using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuraNova.Application.Notifications.DTOs;
using AuraNova.Domain.Enums;

namespace AuraNova.Application.Notifications.Interfaces
{
    public interface INotificationService
    {
        Task NotifyAsync(Guid orderId, NotificationType type, string? reason = null);
        Task<IReadOnlyList<NotificationResponse>> GetByOrderIdAsync(Guid orderId);
        Task<NotificationResponse?> GetByIdAsync(Guid id);
        Task<WhatsAppPreparationResponse?> PrepareWhatsAppAsync(Guid id);
    }
}
