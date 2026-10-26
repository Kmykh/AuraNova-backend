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

        /// <summary>Vuelve a enviar un correo (fallido o no recibido).</summary>
        Task<bool> ResendEmailAsync(Guid notificationId);

        /// <summary>WhatsApp generados que el equipo aún no abrió para enviar (bandeja del panel).</summary>
        Task<IReadOnlyList<NotificationResponse>> GetPendingWhatsAppAsync();
    }
}
