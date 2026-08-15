using System;
using System.Threading.Tasks;
using AuraNova.Application.AdminOrders.DTOs;
using AuraNova.Application.Common.Models;

namespace AuraNova.Application.AdminOrders.Interfaces
{
    public interface IAdminOrderQueryService
    {
        Task<PagedResponse<AdminOrderListItemResponse>> GetOrdersAsync(AdminOrderFilterRequest request);
        Task<AdminOrderDetailResponse?> GetOrderDetailAsync(Guid id);
    }
}
