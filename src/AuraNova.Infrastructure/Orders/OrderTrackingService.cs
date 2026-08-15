using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Orders;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.Orders
{
    public class OrderTrackingService : IOrderTrackingService
    {
        private readonly AppDbContext _db;

        public OrderTrackingService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PublicTrackingResponse?> GetTrackingAsync(string orderCode, string trackingToken)
        {
            var order = await _db.Orders
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null)
                return null;

            // Constant-time-ish comparison to avoid timing attacks
            if (!string.Equals(order.TrackingToken, trackingToken, System.StringComparison.Ordinal))
                return null;

            var history = (order.StatusHistory ?? [])
                .OrderBy(h => h.CreatedAt)
                .ToList();

            var timeline = history.Select(h => new TrackingTimelineItem
            {
                Status = h.Status.ToString(),
                Label = OrderStatusLabels.GetLabel(h.Status),
                Completed = true,
                CreatedAt = h.CreatedAt
            }).ToList();

            return new PublicTrackingResponse
            {
                OrderCode = order.OrderCode,
                Status = order.Status.ToString(),
                StatusLabel = OrderStatusLabels.GetLabel(order.Status),
                DeliveryType = order.DeliveryType.ToString(),
                Total = order.Total,
                Timeline = timeline
            };
        }
    }
}
