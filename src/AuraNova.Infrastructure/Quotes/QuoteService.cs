using AuraNova.Application.Quotes.DTOs;
using AuraNova.Application.Quotes.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
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

        public QuoteService(AppDbContext db, INotificationService notificationService, AuraNova.Application.Auth.Interfaces.ICurrentUserService currentUserService, ILogger<QuoteService> logger)
        {
            _db = db;
            _notificationService = notificationService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<QuoteResponse>> GetAllAsync()
        {
            var quotes = await _db.Quotes
                .Include(q => q.Order)
                    .ThenInclude(o => o!.Customer)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            return quotes.Select(q => MapToResponse(q)).ToList();
        }

        public async Task<QuoteResponse?> GetByIdAsync(Guid id)
        {
            var quote = await _db.Quotes
                .Include(q => q.Order)
                    .ThenInclude(o => o!.Customer)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quote == null) return null;

            return MapToResponse(quote);
        }

        public async Task<QuoteResponse> UpdateAsync(Guid id, UpdateQuoteRequest request)
        {
            var quote = await _db.Quotes
                .Include(q => q.Order)
                    .ThenInclude(o => o!.Customer)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quote == null)
                throw new OrderNotFoundException($"Cotización con Id '{id}' no encontrada.");

            var order = quote.Order!;

            // Validate order is in correct status
            if (order.Status != OrderStatus.WaitingQuote)
                throw new OrderValidationException(
                    $"El pedido '{order.OrderCode}' no está en estado WaitingQuote. Estado actual: {order.Status}.");

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

            // Update Order
            order.CustomizationCost = request.CustomizationCost;
            order.Total = order.Subtotal + (order.DeliveryCost ?? 0m) + request.CustomizationCost;
            order.Status = OrderStatus.QuoteReady;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.QuoteReady,
                Comment = $"Cotización lista. Envío: S/ {(order.DeliveryCost ?? 0m):F2}",
                AdminUserId = _currentUserService.UserId,
                AdminName = _currentUserService.Name
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Cotización {QuoteId} completada para pedido {OrderCode}. Envío: S/ {ShippingCost}",
                quote.Id, order.OrderCode, request.ShippingCost);

            // --- Trigger Notification ---
            await _notificationService.NotifyAsync(order.Id, NotificationType.QuoteReady);

            return MapToResponse(quote);
        }

        private QuoteResponse MapToResponse(Quote quote)
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
                ReferenceImageUrl = order.ReferenceImageUrl,
                CustomizationNotes = order.CustomizationNotes,
                DeliveryType = order.DeliveryType.ToString()
            };

            return response;
        }
    }
}
