using AuraNova.Application.Orders.DTOs;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IOrderService
    {
        Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request);
        Task<bool> AcceptQuoteAsync(Guid orderId);
    }
}
