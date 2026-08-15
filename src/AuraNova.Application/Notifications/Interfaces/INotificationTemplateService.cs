using AuraNova.Domain.Entities;

namespace AuraNova.Application.Notifications.Interfaces
{
    public interface INotificationTemplateService
    {
        Task<string> BuildOrderCreatedMessageAsync(Order order);
        Task<string> BuildQuoteReadyMessageAsync(Order order);
        Task<string> BuildPaymentReportedMessageAsync(Order order);
        Task<string> BuildPaymentConfirmedMessageAsync(Order order);
        Task<string> BuildPaymentRejectedMessageAsync(Order order, string? reason);
        Task<string> BuildPreparingMessageAsync(Order order);
        Task<string> BuildReadyMessageAsync(Order order);
        Task<string> BuildShippedMessageAsync(Order order);
        Task<string> BuildDeliveredMessageAsync(Order order);
        Task<string> BuildCancelledMessageAsync(Order order, string? reason);
    }
}
