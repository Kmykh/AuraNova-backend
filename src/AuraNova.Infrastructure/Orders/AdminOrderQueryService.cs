using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.AdminOrders.DTOs;
using AuraNova.Application.AdminOrders.Interfaces;
using AuraNova.Application.Common.Models;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.Orders
{
    public class AdminOrderQueryService : IAdminOrderQueryService
    {
        private readonly AppDbContext _db;

        public AdminOrderQueryService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PagedResponse<AdminOrderListItemResponse>> GetOrdersAsync(AdminOrderFilterRequest request)
        {
            var query = _db.Set<Order>().AsNoTracking().AsQueryable();

            if (request.Status.HasValue)
            {
                query = query.Where(o => o.Status == request.Status.Value);
            }

            if (request.DeliveryType.HasValue)
            {
                query = query.Where(o => o.DeliveryType == request.DeliveryType.Value);
            }

            if (request.DateFrom.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= request.DateFrom.Value);
            }

            if (request.DateTo.HasValue)
            {
                var dateTo = request.DateTo.Value.Date.AddDays(1);
                query = query.Where(o => o.CreatedAt < dateTo);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.ToLower();
                query = query.Where(o => 
                    o.OrderCode.ToLower().Contains(search) ||
                    (o.Customer != null && o.Customer.Name.ToLower().Contains(search)) ||
                    (o.Customer != null && o.Customer.Phone.Contains(search))
                );
            }

            var totalItems = await query.CountAsync();

            var orderEntities = await query
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var items = orderEntities.Select(o => new AdminOrderListItemResponse
                {
                    Id = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.Customer != null ? o.Customer.Name : string.Empty,
                    CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
                    DeliveryType = o.DeliveryType.ToString(),
                    Status = o.Status.ToString(),
                    Subtotal = o.Subtotal,
                    DeliveryCost = o.DeliveryCost,
                    Total = o.Total,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt
                }).ToList();

            return new PagedResponse<AdminOrderListItemResponse>(items, totalItems, request.Page, request.PageSize);
        }

        public async Task<AdminOrderDetailResponse?> GetOrderDetailAsync(Guid id)
        {
            var order = await _db.Set<Order>()
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Items!).ThenInclude(i => i.Product)
                .Include(o => o.DeliveryZone)
                .Include(o => o.MeetingPoint)
                .Include(o => o.Quote)
                .Include(o => o.Payment)
                .Include(o => o.StatusHistory)
                .Include(o => o.Notifications)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return null;

            var response = new AdminOrderDetailResponse
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                Status = order.Status.ToString(),
                DeliveryType = order.DeliveryType.ToString(),
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Subtotal = order.Subtotal,
                DeliveryCost = order.DeliveryCost,
                Total = order.Total,
                HasPaymentEvidence = order.Payment != null && !string.IsNullOrWhiteSpace(order.Payment.EvidenceUrl),
                Customer = new AdminOrderCustomer
                {
                    Name = order.Customer?.Name ?? string.Empty,
                    Phone = order.Customer?.Phone ?? string.Empty,
                    Email = order.Customer?.Email
                },
                Delivery = new AdminOrderDelivery
                {
                    DeliveryZone = order.DeliveryZone?.Name,
                    MeetingPoint = order.MeetingPoint?.Name,
                    DeliveryAddress = order.DeliveryAddress,
                    Department = order.Department,
                    Province = order.Province,
                    District = order.District
                },
                Items = order.Items?.Select(i => new AdminOrderItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? string.Empty,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Subtotal
                }).ToList() ?? new List<AdminOrderItem>(),
                StatusHistory = order.StatusHistory?.OrderByDescending(h => h.CreatedAt).Select(h => new AdminOrderStatusHistory
                {
                    Status = h.Status.ToString(),
                    Comment = h.Comment,
                    CreatedAt = h.CreatedAt
                }).ToList() ?? new List<AdminOrderStatusHistory>(),
                Notifications = order.Notifications?.OrderByDescending(n => n.CreatedAt).Select(n => new AdminOrderNotification
                {
                    Type = n.Type.ToString(),
                    Channel = n.Channel.ToString(),
                    Status = n.Status.ToString(),
                    CreatedAt = n.CreatedAt
                }).ToList() ?? new List<AdminOrderNotification>()
            };

            if (order.Quote != null)
            {
                response.Quote = new AdminOrderQuote
                {
                    QuoteStatus = order.Quote.Status.ToString(),
                    ShippingCost = order.Quote.ShippingCost ?? 0m,
                    Notes = order.Quote.Notes,
                    QuotedAt = order.Quote.QuotedAt
                };
            }

            if (order.Payment != null)
            {
                response.Payment = new AdminOrderPayment
                {
                    PaymentStatus = order.Payment.Status.ToString(),
                    PaymentMethod = order.Payment.Method.ToString(),
                    Amount = order.Payment.Amount,
                    CreatedAt = order.Payment.CreatedAt,
                    VerifiedAt = order.Payment.VerifiedAt
                };
            }

            return response;
        }
    }
}
