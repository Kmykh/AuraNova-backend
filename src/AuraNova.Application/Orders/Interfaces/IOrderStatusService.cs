using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Enums;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IOrderStatusService
    {
        Task<OrderStatusChangeResponse> ChangeStatusAsync(Guid orderId, OrderStatus newStatus, string? comment);
        Task<IReadOnlyList<OrderStatusHistoryResponse>> GetHistoryAsync(Guid orderId);
    }
}
