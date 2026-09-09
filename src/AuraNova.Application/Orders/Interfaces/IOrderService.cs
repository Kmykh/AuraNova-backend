using AuraNova.Application.Orders.DTOs;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IOrderService
    {
        Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request);
        Task<CreateOrderResponse> CreateCustomAsync(CreateCustomOrderRequest request);
        Task CancelAsync(Guid id, string reason);
        
        // Administrative actions
        Task StartPreparationAsync(Guid id);
        Task SetEstimatedReadyDateAsync(Guid id, DateTimeOffset estimatedDate);
        Task MarkAsReadyAsync(Guid id);
        Task DeliverToAgencyAsync(Guid id, string provider, string trackingCode, string? proofUrl);
        Task<bool> AcceptQuoteAsync(Guid orderId);
    }
}
