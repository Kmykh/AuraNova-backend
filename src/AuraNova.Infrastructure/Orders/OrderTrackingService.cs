using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Orders;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.Orders
{
    public class OrderTrackingService : IOrderTrackingService
    {
        private readonly AppDbContext _db;
        private readonly IFileStorageService _storage;

        public OrderTrackingService(AppDbContext db, IFileStorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task<PublicTrackingResponse?> GetTrackingAsync(string orderCode, string trackingToken)
        {
            var order = await OrdersWithDetails().FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null)
                return null;

            // Constant-time-ish comparison to avoid timing attacks
            if (!string.Equals(order.TrackingToken, trackingToken, System.StringComparison.Ordinal))
                return null;

            return await MapAsync(order);
        }

        public async Task<PublicTrackingResponse?> GetTrackingByCodeAsync(string trackingCode)
        {
            var code = OrderCodes.NormalizeTrackingCode(trackingCode);
            if (code == null)
                return null;

            var order = await OrdersWithDetails().FirstOrDefaultAsync(o => o.TrackingToken == code);
            return order == null ? null : await MapAsync(order);
        }

        public async Task<OrderPaymentSummaryResponse?> GetPaymentSummaryAsync(System.Guid orderId)
        {
            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items!).ThenInclude(i => i.Product)
                .Include(o => o.Quote)
                .Include(o => o.Payment)
                .Include(o => o.DeliveryZone)
                .Include(o => o.MeetingPoint)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return null;

            return new OrderPaymentSummaryResponse
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                TrackingCode = order.TrackingToken,
                Status = order.Status.ToString(),
                StatusLabel = OrderStatusLabels.GetLabel(order.Status),
                DeliveryType = order.DeliveryType.ToString(),
                DeliveryPlace = order.DeliveryType switch
                {
                    Domain.Enums.DeliveryType.MeetingPoint => order.MeetingPoint?.Name,
                    Domain.Enums.DeliveryType.Delivery => order.DeliveryZone?.Name,
                    _ => string.Join(", ", new[] { order.District, order.Province, order.Department }.Where(p => !string.IsNullOrWhiteSpace(p)))
                },
                IsCustomOrder = order.IsCustomOrder,
                Subtotal = order.Subtotal,
                DeliveryCost = order.DeliveryCost,
                CustomizationCost = order.CustomizationCost,
                Total = order.Total,
                QuoteNotes = order.Quote?.Notes,
                QuotedAt = order.Quote?.QuotedAt,
                PaymentStatus = order.Payment?.Status.ToString(),
                PaymentAmount = order.Payment?.Amount,
                AmountPaid = order.Payment?.AmountPaid ?? 0m,
                BalanceDue = Math.Max(0, (order.Payment?.Amount ?? order.Total ?? 0m) - (order.Payment?.AmountPaid ?? 0m)),
                DepositAmount = order.Total is > 0 && (order.Payment?.AmountPaid ?? 0m) <= 0
                    ? Payments.OnlinePaymentService.DepositFor(order.Payment?.Amount ?? order.Total.Value)
                    : null,
                PaymentRejectionReason = order.Payment?.Status == Domain.Enums.PaymentStatus.Rejected ? order.Payment.Notes : null,
                CreatedAt = order.CreatedAt,
                Items = MapItems(order)
            };
        }

        private static List<PublicTrackingItemResponse> MapItems(Order order) => order.Items == null
            ? new List<PublicTrackingItemResponse>()
            : order.Items.Select(i => new PublicTrackingItemResponse
            {
                ProductName = i.Product?.Name ?? "Producto Desconocido",
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                ImageUrl = i.Product?.ImageUrl,
                SelectedColors = i.SelectedColors ?? [],
                SelectedFlowerTypes = i.SelectedFlowerTypes ?? [],
                HasLights = i.HasLights,
                HasButterfly = i.HasButterfly,
                Accessories = AccessoryCatalog.ForItem(i),
                HasPhraseCard = i.HasPhraseCard,
                PhraseText = i.PhraseText
            }).ToList();

        /// <summary>Notas de devolución que escribe el sistema (incluida la que le pide al equipo devolver un pago).</summary>
        private static bool IsRefundNote(string? comment) =>
            comment != null && (comment.StartsWith("Devolución", StringComparison.OrdinalIgnoreCase) ||
                                comment.StartsWith("Se aprobó un pago de", StringComparison.OrdinalIgnoreCase));

        /// <summary>Un cambio del historial con un título que el cliente entiende (los pagos con su propio título).</summary>
        private static TrackingTimelineItem TimelineItem(OrderStatusHistory h, Order order)
        {
            var comment = h.Comment?.Trim();
            var item = new TrackingTimelineItem
            {
                Status = h.Status.ToString(),
                Label = OrderStatusLabels.GetLabel(h.Status),
                Description = string.IsNullOrWhiteSpace(comment) ? OrderStatusDescriptions.GetDescription(h.Status, order) : comment,
                Completed = true,
                CreatedAt = h.CreatedAt,
                Kind = h.Status == Domain.Enums.OrderStatus.Cancelled ? "cancelled" : "status"
            };

            if (string.IsNullOrEmpty(comment))
                return item;

            if (comment.Contains("rechazada", StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "PaymentRejected";
                item.Kind = "rejection";
                item.Label = "Pago rechazado";
            }
            else if (comment.StartsWith("Adelanto pagado", StringComparison.OrdinalIgnoreCase))
            {
                item.Kind = "payment";
                item.Label = "Adelanto pagado";
            }
            else if (comment.StartsWith("Saldo pagado", StringComparison.OrdinalIgnoreCase))
            {
                item.Kind = "payment";
                item.Label = "Saldo pagado: pedido pagado por completo";
            }
            else if (comment.StartsWith("Pago total", StringComparison.OrdinalIgnoreCase))
            {
                item.Kind = "payment";
                item.Label = "Pago recibido";
            }
            else if (comment.StartsWith("Pago confirmado por administrador", StringComparison.OrdinalIgnoreCase))
            {
                item.Kind = "payment";
                item.Label = "Pago confirmado";
                item.Description = "Revisamos y confirmamos tu pago.";
            }
            else if (h.Status == Domain.Enums.OrderStatus.Cancelled)
            {
                var reason = comment.StartsWith("Cancelado:", StringComparison.OrdinalIgnoreCase) ? comment["Cancelado:".Length..].Trim() : comment;
                item.Description = reason.Length > 0 ? $"Motivo: {reason}" : OrderStatusDescriptions.GetDescription(h.Status, order);
            }

            return item;
        }

        private IQueryable<Order> OrdersWithDetails() => _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.StatusHistory)
            .Include(o => o.Items!)
            .ThenInclude(i => i.Product)
            .Include(o => o.DeliveryZone)
            .Include(o => o.MeetingPoint)
            .Include(o => o.Payment!).ThenInclude(p => p.Transactions)
            .Include(o => o.ReferenceImages);

        private async Task<PublicTrackingResponse> MapAsync(Order order)
        {
            var history = (order.StatusHistory ?? [])
                .OrderBy(h => h.CreatedAt)
                .ToList();

            // Las devoluciones se muestran desde los cobros (monto, medio y fecha) y no con la nota del equipo
            var timeline = history
                .Where(h => !IsRefundNote(h.Comment))
                .Select(h => TimelineItem(h, order))
                .ToList();

            foreach (var t in (order.Payment?.Transactions ?? []).Where(t => t.ApprovedAt != null && t.RefundedAmount > 0))
            {
                timeline.Add(new TrackingTimelineItem
                {
                    Status = "Refunded",
                    Kind = "refund",
                    Label = "Te devolvimos tu dinero",
                    Description = $"S/ {t.RefundedAmount:F2} a tu {TrackingPayments.MethodLabel(t)}.",
                    Completed = true,
                    CreatedAt = t.RefundedAt ?? t.UpdatedAt
                });
            }
            timeline = timeline.OrderBy(i => i.CreatedAt ?? DateTimeOffset.MinValue).ToList();

            // Only the owner (tracking code) gets these signed, short-lived URLs
            var fileUrls = await _storage.GetReadUrlsAsync(
                CustomOrderBrief.StoredPaths(order).Append(order.ShippingProofUrl).OfType<string>());
            string? FileUrl(string? stored) => stored == null ? null : fileUrls.GetValueOrDefault(stored);

            string? customerFirstName = null;
            if (!string.IsNullOrWhiteSpace(order.Customer?.Name))
            {
                customerFirstName = order.Customer.Name.Split(' ').FirstOrDefault();
            }

            return new PublicTrackingResponse
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                CustomerFirstName = customerFirstName,
                Status = order.Status.ToString(),
                StatusLabel = OrderStatusLabels.GetLabel(order.Status),
                DeliveryType = order.DeliveryType.ToString(),
                
                IsCustomOrder = order.IsCustomOrder,
                ReferenceImageUrl = FileUrl(order.ReferenceImageUrl),
                CustomizationNotes = order.CustomizationNotes,
                Notes = order.Notes,
                ReferenceImages = CustomOrderBrief.ImageUrls(order, fileUrls),
                ReferenceLinks = order.ReferenceLinks ?? [],
                DesiredDate = order.DesiredDate,
                Occasion = order.Occasion,
                BudgetRange = order.BudgetRange,

                Payment = TrackingPayments.Build(order, DateTimeOffset.UtcNow),
                Costs = new PublicTrackingCosts
                {
                    Subtotal = order.Subtotal,
                    DeliveryCost = order.DeliveryCost,
                    CustomizationCost = order.CustomizationCost,
                    Total = order.Total,
                    AmountPaid = order.Payment?.AmountPaid ?? 0m,
                    // Solo hay saldo cuando ya se pagó el adelanto
                    BalanceDue = order.Payment is { AmountPaid: > 0 } paid ? Math.Max(0, paid.Amount - paid.AmountPaid) : 0m
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
                    ProofUrl = FileUrl(order.ShippingProofUrl)
                } : null,

                Timeline = timeline,
                Items = MapItems(order),
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
