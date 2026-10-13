using AuraNova.Application.Orders;
using AuraNova.Application.Quotes.DTOs;
using AuraNova.Application.Quotes.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Quotes
{
    public class QuoteService : IQuoteService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notificationService;
        private readonly ILogger<QuoteService> _logger;
        private readonly AuraNova.Application.Auth.Interfaces.ICurrentUserService _currentUserService;
        private readonly IFileStorageService _storage;

        public QuoteService(AppDbContext db, INotificationService notificationService, AuraNova.Application.Auth.Interfaces.ICurrentUserService currentUserService, ILogger<QuoteService> logger, IFileStorageService storage)
        {
            _db = db;
            _notificationService = notificationService;
            _currentUserService = currentUserService;
            _logger = logger;
            _storage = storage;
        }

        public async Task<IReadOnlyList<QuoteResponse>> GetAllAsync()
        {
            var quotes = await _db.Quotes
                .Include(q => q.Order)
                    .ThenInclude(o => o!.Customer)
                .Include(q => q.Order)
                    .ThenInclude(o => o!.ReferenceImages)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            var imageUrls = await _storage.GetReadUrlsAsync(quotes
                .Where(q => q.Order != null)
                .SelectMany(q => CustomOrderBrief.StoredPaths(q.Order!).Append(q.Order!.ReferenceImageUrl))
                .OfType<string>());

            return quotes.Select(q => MapToResponse(q, imageUrls)).ToList();
        }

        public async Task<QuoteResponse?> GetByIdAsync(Guid id)
        {
            var quote = await _db.Quotes
                .Include(q => q.Order)
                    .ThenInclude(o => o!.Customer)
                .Include(q => q.Order)
                    .ThenInclude(o => o!.ReferenceImages)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quote == null) return null;

            return await MapToResponseAsync(quote);
        }

        public async Task<QuoteResponse> UpdateAsync(Guid id, UpdateQuoteRequest request)
        {
            var quote = await _db.Quotes
                .Include(q => q.Order)
                    .ThenInclude(o => o!.Customer)
                .Include(q => q.Order)
                    .ThenInclude(o => o!.ReferenceImages)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quote == null)
                throw new OrderNotFoundException($"Cotización con Id '{id}' no encontrada.");

            var order = quote.Order!;

            // Se puede cotizar y también corregir el precio mientras el cliente no lo haya aceptado
            if (order.Status != OrderStatus.WaitingQuote && order.Status != OrderStatus.QuoteReady)
                throw new OrderValidationException(
                    $"El pedido '{order.OrderCode}' ya no admite cambios de precio. Estado actual: {OrderStatusLabels.GetLabel(order.Status)}.");

            var isRequote = order.Status == OrderStatus.QuoteReady;

            // Validate shipping cost
            if (request.ShippingCost < 0)
                throw new OrderValidationException("El costo de envío no puede ser negativo.");

            if (request.CustomizationCost < 0)
                throw new OrderValidationException("El costo de personalización no puede ser negativo.");

            // Shipping cost logic is deprecated. DeliveryType handles costs directly.
            // For NationalShipping, cost is paid at destination (null).
            // For Delivery, cost is set at creation from DeliveryZone.
            // For MeetingPoint, cost is 0.
            quote.ShippingCost = order.DeliveryCost ?? 0m;

            quote.CustomizationCost = request.CustomizationCost;
            quote.Notes = request.Notes?.Trim();
            quote.Status = QuoteStatus.Ready;
            quote.QuotedAt = DateTimeOffset.UtcNow;
            quote.UpdatedAt = DateTimeOffset.UtcNow;

            // Update Order: el total queda guardado en el pedido (es lo que ven el cliente, el pago y el panel)
            var total = order.Subtotal + (order.DeliveryCost ?? 0m) + request.CustomizationCost;
            if (total <= 0)
                throw new OrderValidationException("Ingresa el precio del pedido: el total no puede ser S/ 0.");

            order.CustomizationCost = request.CustomizationCost;
            order.Total = total;
            order.Status = OrderStatus.QuoteReady;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.QuoteReady,
                Comment = isRequote
                    ? $"Cotización actualizada. Nuevo total: S/ {total:F2}"
                    : $"Cotización lista. Total: S/ {total:F2}",
                AdminUserId = _currentUserService.UserId,
                AdminName = _currentUserService.Name
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Cotización {QuoteId} completada para pedido {OrderCode}. Envío: S/ {ShippingCost}",
                quote.Id, order.OrderCode, request.ShippingCost);

            // --- Trigger Notification ---
            await _notificationService.NotifyAsync(order.Id, NotificationType.QuoteReady);

            return await MapToResponseAsync(quote);
        }

        private async Task<QuoteResponse> MapToResponseAsync(Quote quote)
        {
            var imageUrls = await _storage.GetReadUrlsAsync(quote.Order == null
                ? []
                : CustomOrderBrief.StoredPaths(quote.Order).Append(quote.Order.ReferenceImageUrl).OfType<string>());
            return MapToResponse(quote, imageUrls);
        }

        private static QuoteResponse MapToResponse(Quote quote, IReadOnlyDictionary<string, string> imageUrls)
        {
            var order = quote.Order!;
            var response = new QuoteResponse
            {
                QuoteId = quote.Id,
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                ShippingCost = quote.ShippingCost,
                CustomizationCost = quote.CustomizationCost,
                Subtotal = order.Subtotal,
                Total = order.Total,
                Notes = quote.Notes,
                Status = quote.Status.ToString(),
                OrderStatus = order.Status.ToString(),
                CreatedAt = quote.CreatedAt,
                QuotedAt = quote.QuotedAt,
                CustomerName = order.Customer?.Name ?? "",
                CustomerPhone = order.Customer?.Phone ?? "",
                IsCustomOrder = order.IsCustomOrder,
                // Signed URL: reference images live in the private bucket
                ReferenceImageUrl = order.ReferenceImageUrl == null ? null : imageUrls.GetValueOrDefault(order.ReferenceImageUrl),
                CustomizationNotes = order.CustomizationNotes,
                DeliveryType = order.DeliveryType.ToString(),
                ReferenceImages = CustomOrderBrief.ImageUrls(order, imageUrls),
                ReferenceLinks = order.ReferenceLinks ?? [],
                DesiredDate = order.DesiredDate,
                Occasion = order.Occasion,
                BudgetRange = order.BudgetRange
            };

            return response;
        }
    }
}
