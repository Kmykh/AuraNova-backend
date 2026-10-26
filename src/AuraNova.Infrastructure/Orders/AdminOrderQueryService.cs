using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.AdminOrders.DTOs;
using AuraNova.Application.AdminOrders.Interfaces;
using AuraNova.Application.Common.Models;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.Orders
{
    public class AdminOrderQueryService : IAdminOrderQueryService
    {
        private readonly AppDbContext _db;
        private readonly IFileStorageService _storage;

        public AdminOrderQueryService(AppDbContext db, IFileStorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task<PagedResponse<AdminOrderListItemResponse>> GetOrdersAsync(AdminOrderFilterRequest request)
        {
            var query = _db.Set<Order>().AsNoTracking().AsQueryable();

            if (request.Status.HasValue)
            {
                query = query.Where(o => o.Status == request.Status.Value);
            }
            else if (ViewStatuses(request.View) is { } statuses)
            {
                query = query.Where(o => statuses.Contains(o.Status));
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
                var search = request.Search.Trim().ToLower();
                var trackingCode = OrderCodes.NormalizeTrackingCode(request.Search);
                query = query.Where(o =>
                    o.OrderCode.ToLower().Contains(search) ||
                    (trackingCode != null && o.TrackingToken == trackingCode) ||
                    (o.Customer != null && o.Customer.Name.ToLower().Contains(search)) ||
                    (o.Customer != null && o.Customer.Phone.Contains(search)) ||
                    o.Items!.Any(i => i.Product != null && i.Product.Name.ToLower().Contains(search))
                );
            }

            var totalItems = await query.CountAsync();

            // Proyección: no se cargan columnas pesadas (p. ej. ReferenceImageUrl) que el listado no usa.
            var rows = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(o => new
                {
                    o.Id,
                    o.OrderCode,
                    CustomerName = o.Customer != null ? o.Customer.Name : string.Empty,
                    CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
                    o.DeliveryType,
                    o.TrackingToken,
                    o.Status,
                    o.Subtotal,
                    o.DeliveryCost,
                    o.CustomizationCost,
                    o.Total,
                    o.IsCustomOrder,
                    o.CreatedAt,
                    o.UpdatedAt,
                    o.StartedAt,
                    o.EstimatedReadyAt,
                    o.DesiredDate,
                    o.Occasion,
                    ZoneName = o.DeliveryZone != null ? o.DeliveryZone.Name : null,
                    MeetingPointName = o.MeetingPoint != null ? o.MeetingPoint.Name : null,
                    o.District,
                    PaymentStatus = o.Payment != null ? (PaymentStatus?)o.Payment.Status : null,
                    BalanceDue = o.Payment != null && o.Payment.AmountPaid > 0 && o.Payment.AmountPaid < o.Payment.Amount
                        ? (decimal?)(o.Payment.Amount - o.Payment.AmountPaid)
                        : null,
                    ReferenceImageCount = o.ReferenceImages!.Count(),
                    Items = o.Items!
                        .Select(i => new
                        {
                            i.Quantity,
                            ProductName = i.Product != null ? i.Product.Name : string.Empty,
                            ImageUrl = i.Product != null ? i.Product.ImageUrl : null,
                            i.SelectedColors,
                            HasExtras = i.HasLights || i.HasButterfly || i.HasPhraseCard
                        })
                        .ToList()
                })
                .ToListAsync();

            var items = rows.Select(o => new AdminOrderListItemResponse
                {
                    Id = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    DeliveryType = o.DeliveryType.ToString(),
                    TrackingToken = o.TrackingToken,
                    Status = o.Status.ToString(),
                    Subtotal = o.Subtotal,
                    DeliveryCost = o.DeliveryCost,
                    CustomizationCost = o.CustomizationCost,
                    Total = o.Total,
                    IsCustomOrder = o.IsCustomOrder,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt,
                    StartedAt = o.StartedAt,
                    EstimatedReadyAt = o.EstimatedReadyAt,
                    DesiredDate = o.DesiredDate,
                    Occasion = o.Occasion,
                    DeliveryPlace = o.DeliveryType switch
                    {
                        DeliveryType.MeetingPoint => o.MeetingPointName,
                        DeliveryType.Delivery => o.ZoneName,
                        _ => o.District
                    },
                    PaymentStatus = o.PaymentStatus?.ToString(),
                    BalanceDue = o.BalanceDue,
                    ReferenceImageCount = o.ReferenceImageCount,
                    ItemCount = o.Items.Sum(i => i.Quantity),
                    Items = o.Items.Select(i => new AdminOrderListProduct
                    {
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        ImageUrl = i.ImageUrl,
                        Colors = (i.SelectedColors ?? []).Take(4).ToList(),
                        HasExtras = i.HasExtras
                    }).ToList()
                }).ToList();

            return new PagedResponse<AdminOrderListItemResponse>(items, totalItems, request.Page, request.PageSize);
        }

        public async Task<AdminOrderCountsResponse> GetCountsAsync()
        {
            var counts = await _db.Set<Order>()
                .AsNoTracking()
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            return new AdminOrderCountsResponse
            {
                ByStatus = counts.ToDictionary(c => c.Status.ToString(), c => c.Count),
                Total = counts.Sum(c => c.Count)
            };
        }

        /// <summary>Estados que agrupa cada pestaña del panel.</summary>
        private static OrderStatus[]? ViewStatuses(string? view) => view?.Trim().ToLowerInvariant() switch
        {
            "action" => [OrderStatus.WaitingQuote, OrderStatus.PaymentReported, OrderStatus.PaymentConfirmed],
            "customer" => [OrderStatus.QuoteReady, OrderStatus.WaitingPayment],
            "workshop" => [OrderStatus.Preparing],
            "delivery" => [OrderStatus.Ready, OrderStatus.Shipped, OrderStatus.DeliveredToAgency],
            "done" => [OrderStatus.Delivered, OrderStatus.Cancelled],
            _ => null
        };

        public async Task<AdminOrderDetailResponse?> GetOrderDetailAsync(Guid id)
        {
            var order = await _db.Set<Order>()
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Items!).ThenInclude(i => i.Product)
                .Include(o => o.DeliveryZone)
                .Include(o => o.MeetingPoint)
                .Include(o => o.Quote)
                .Include(o => o.Payment!).ThenInclude(p => p.Transactions)
                .Include(o => o.StatusHistory)
                .Include(o => o.Notifications)
                .Include(o => o.ReferenceImages)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return null;

            // Customer files (reference, shipping proof, payment evidence) are private: signed, short-lived URLs
            var fileUrls = await _storage.GetReadUrlsAsync(CustomOrderBrief.StoredPaths(order)
                .Concat(new[] { order.ReferenceImageUrl, order.ShippingProofUrl, order.Payment?.EvidenceUrl }.OfType<string>())
                .Concat((order.Items ?? []).Select(i => i.CardBackgroundPath).OfType<string>()));
            string? FileUrl(string? stored) => stored == null ? null : fileUrls.GetValueOrDefault(stored);

            var response = new AdminOrderDetailResponse
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                Status = order.Status.ToString(),
                DeliveryType = order.DeliveryType.ToString(),
                TrackingToken = order.TrackingToken,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Subtotal = order.Subtotal,
                DeliveryCost = order.DeliveryCost,
                CustomizationCost = order.CustomizationCost,
                Total = order.Total,
                HasPaymentEvidence = order.Payment != null && !string.IsNullOrWhiteSpace(order.Payment.EvidenceUrl),
                IsCustomOrder = order.IsCustomOrder,
                ReferenceImageUrl = FileUrl(order.ReferenceImageUrl),
                CustomizationNotes = order.CustomizationNotes,
                ReferenceImages = CustomOrderBrief.ImageUrls(order, fileUrls),
                ReferenceLinks = order.ReferenceLinks ?? [],
                DesiredDate = order.DesiredDate,
                Occasion = order.Occasion,
                BudgetRange = order.BudgetRange,
                StartedAt = order.StartedAt,
                EstimatedReadyAt = order.EstimatedReadyAt,
                ReadyAt = order.ReadyAt,
                DeliveredToAgencyAt = order.DeliveredToAgencyAt,
                ShippingProvider = order.ShippingProvider,
                ShippingTrackingCode = order.ShippingTrackingCode,
                ShippingProofUrl = FileUrl(order.ShippingProofUrl),
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
                    District = order.District,
                    RecipientName = order.RecipientName,
                    RecipientPhone = order.RecipientPhone,
                    RecipientDocument = order.RecipientDocument,
                    DeliveryReference = order.DeliveryReference,
                    PreferredAgency = order.PreferredAgency
                },
                Items = order.Items?.Select(i => new AdminOrderItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? string.Empty,
                    ImageUrl = i.Product?.ImageUrl,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Subtotal,
                    SelectedColors = i.SelectedColors ?? [],
                    SelectedFlowerTypes = i.SelectedFlowerTypes ?? [],
                    SelectedPrimaryColor = i.SelectedPrimaryColor,
                    SelectedSecondaryColor = i.SelectedSecondaryColor,
                    SelectedFlowerType = i.SelectedFlowerType,
                    SelectedFlowerColor = i.SelectedFlowerColor,
                    HasLights = i.HasLights,
                    HasButterfly = i.HasButterfly,
                    HasPhraseCard = i.HasPhraseCard,
                    PhraseText = i.PhraseText,
                    PhraseFont = i.PhraseFont,
                    PhraseTo = i.PhraseTo,
                    PhraseFrom = i.PhraseFrom,
                    PhraseTheme = i.PhraseTheme,
                    CardBackgroundUrl = FileUrl(i.CardBackgroundPath),
                    AppliedCampaignName = i.AppliedCampaignName,
                    AppliedCampaignStageName = i.AppliedCampaignStageName
                }).ToList() ?? new List<AdminOrderItem>(),
                StatusHistory = order.StatusHistory?.OrderByDescending(h => h.CreatedAt).Select(h => new AdminOrderStatusHistory
                {
                    Status = h.Status.ToString(),
                    Comment = h.Comment,
                    AdminName = h.AdminName,
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
                    QuoteId = order.Quote.Id,
                    QuoteStatus = order.Quote.Status.ToString(),
                    ShippingCost = order.Quote.ShippingCost ?? 0m,
                    CustomizationCost = order.Quote.CustomizationCost,
                    Notes = order.Quote.Notes,
                    QuotedAt = order.Quote.QuotedAt
                };
            }

            if (order.Payment != null)
            {
                response.Payment = new AdminOrderPayment
                {
                    Id = order.Payment.Id,
                    PaymentStatus = order.Payment.Status.ToString(),
                    PaymentMethod = order.Payment.Method.ToString(),
                    Amount = order.Payment.Amount,
                    AmountPaid = order.Payment.AmountPaid,
                    BalanceDue = Math.Max(0, order.Payment.Amount - order.Payment.AmountPaid),
                    EvidenceUrl = FileUrl(order.Payment.EvidenceUrl),
                    CreatedAt = order.Payment.CreatedAt,
                    VerifiedAt = order.Payment.VerifiedAt,
                    Transactions = (order.Payment.Transactions ?? [])
                        .Where(t => t.Status != "created")
                        .OrderByDescending(t => t.CreatedAt)
                        .Select(t => new AdminPaymentTransaction
                        {
                            Id = t.Id,
                            MethodId = t.MethodId,
                            MethodType = t.MethodType,
                            Amount = t.Amount,
                            IsDeposit = t.IsDeposit,
                            Status = t.Status,
                            StatusDetail = t.StatusDetail,
                            Approved = t.ApprovedAt != null,
                            ExternalOrderId = t.ExternalOrderId,
                            RefundedAmount = t.RefundedAmount,
                            RefundedAt = t.RefundedAt,
                            CreatedAt = t.CreatedAt
                        }).ToList()
                };
            }

            return response;
        }
    }
}
