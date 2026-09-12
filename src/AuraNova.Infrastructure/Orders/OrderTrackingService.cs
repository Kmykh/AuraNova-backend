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
                .Include(o => o.Customer)
                .Include(o => o.StatusHistory)
                .Include(o => o.Items!)
                .ThenInclude(i => i.Product)
                .Include(o => o.DeliveryZone)
                .Include(o => o.MeetingPoint)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null)
                return null;

            // Constant-time-ish comparison to avoid timing attacks
            if (!string.Equals(order.TrackingToken, trackingToken, System.StringComparison.Ordinal))
                return null;

            var history = (order.StatusHistory ?? [])
                .OrderBy(h => h.CreatedAt)
                .ToList();

            var timeline = history.Select(h =>
            {
                var isRejection = !string.IsNullOrWhiteSpace(h.Comment) &&
                                  h.Comment.Contains("rechazada", System.StringComparison.OrdinalIgnoreCase);

                var label = isRejection ? "Pago rechazado" : OrderStatusLabels.GetLabel(h.Status);
                var description = !string.IsNullOrWhiteSpace(h.Comment)
                    ? h.Comment
                    : OrderStatusDescriptions.GetDescription(h.Status, order);

                return new TrackingTimelineItem
                {
                    Status = isRejection ? "PaymentRejected" : h.Status.ToString(),
                    Label = label,
                    Description = description,
                    Completed = true,
                    CreatedAt = h.CreatedAt
                };
            }).ToList();

            string? customerFirstName = null;
            if (!string.IsNullOrWhiteSpace(order.Customer?.Name))
            {
                customerFirstName = order.Customer.Name.Split(' ').FirstOrDefault();
            }

            return new PublicTrackingResponse
            {
                OrderCode = order.OrderCode,
                CustomerFirstName = customerFirstName,
                Status = order.Status.ToString(),
                StatusLabel = OrderStatusLabels.GetLabel(order.Status),
                DeliveryType = order.DeliveryType.ToString(),
                
                IsCustomOrder = order.IsCustomOrder,
                ReferenceImageUrl = order.ReferenceImageUrl,
                CustomizationNotes = order.CustomizationNotes,
                Notes = order.Notes,

                Costs = new PublicTrackingCosts
                {
                    Subtotal = order.Subtotal,
                    DeliveryCost = order.DeliveryCost,
                    CustomizationCost = order.CustomizationCost,
                    Total = order.Total
                },

                Estimates = new PublicTrackingEstimates
                {
                    CreatedAt = order.CreatedAt,
                    StartedAt = order.StartedAt,
                    EstimatedReadyAt = order.EstimatedReadyAt
                },

                NationalShippingDetails = order.DeliveryType == Domain.Enums.DeliveryType.NationalShipping ? new PublicTrackingNationalShipping
                {
                    Provider = order.ShippingProvider,
                    TrackingCode = order.ShippingTrackingCode,
                    ProofUrl = order.ShippingProofUrl
                } : null,

                Timeline = timeline,
                Items = order.Items?.Select(i => new PublicTrackingItemResponse
                {
                    ProductName = i.Product?.Name ?? "Producto Desconocido",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    ImageUrl = i.Product?.ImageUrl
                }).ToList() ?? new List<PublicTrackingItemResponse>(),
                Delivery = new PublicTrackingDeliveryResponse
                {
                    DeliveryZoneName = order.DeliveryZone?.Name,
                    MeetingPointName = order.MeetingPoint?.Name,
                    DeliveryAddress = order.DeliveryAddress,
                    Department = order.Department,
                    Province = order.Province,
                    District = order.District
                }
            };
        }
    }
}
