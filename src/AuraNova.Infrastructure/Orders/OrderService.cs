using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Orders
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notificationService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(AppDbContext db, INotificationService notificationService, ILogger<OrderService> logger)
        {
            _db = db;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request)
        {
            // --- 1. Validate items not empty ---
            if (request.Items == null || request.Items.Count == 0)
                throw new OrderValidationException("El pedido debe contener al menos un producto.");

            // --- 2. Reject duplicate ProductIds ---
            var duplicateIds = request.Items
                .GroupBy(i => i.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
                throw new OrderValidationException(
                    $"El pedido contiene productos duplicados: {string.Join(", ", duplicateIds)}. Envíe cada producto una sola vez con la cantidad total.");

            // --- 3. Validate each item quantity ---
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                    throw new OrderValidationException(
                        $"La cantidad del producto {item.ProductId} debe ser mayor a 0.");
            }

            // --- 4. Lookup products from DB ---
            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            // --- 5. Validate all products exist ---
            var foundIds = products.Select(p => p.Id).ToHashSet();
            var missingIds = productIds.Where(id => !foundIds.Contains(id)).ToList();
            if (missingIds.Count > 0)
                throw new OrderNotFoundException(
                    $"Productos no encontrados: {string.Join(", ", missingIds)}.");

            // --- 6. Validate availability and stock ---
            foreach (var item in request.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);

                if (!product.IsAvailable)
                    throw new OrderValidationException(
                        $"El producto '{product.Name}' no está disponible actualmente.");

                if (product.Stock < item.Quantity)
                    throw new OrderValidationException(
                        $"Stock insuficiente para '{product.Name}'. Disponible: {product.Stock}, solicitado: {item.Quantity}.");
            }

            // --- 7. Validate delivery info ---
            if (request.Delivery == null)
                throw new OrderValidationException("La información de entrega es obligatoria.");

            if (!Enum.TryParse<DeliveryType>(request.Delivery.Type, ignoreCase: true, out var deliveryType))
                throw new OrderValidationException(
                    $"Tipo de entrega inválido: '{request.Delivery.Type}'. Valores válidos: Delivery, MeetingPoint, NationalShipping.");

            // Delivery-type-specific validation and cost resolution
            decimal? deliveryCost = null;
            DeliveryZone? deliveryZone = null;
            MeetingPoint? meetingPoint = null;
            OrderStatus initialStatus;
            string? deliveryZoneName = null;
            string? meetingPointName = null;

            switch (deliveryType)
            {
                case DeliveryType.Delivery:
                    if (request.Delivery.DeliveryZoneId == null)
                        throw new OrderValidationException("El campo DeliveryZoneId es obligatorio para tipo Delivery.");

                    if (string.IsNullOrWhiteSpace(request.Delivery.DeliveryAddress))
                        throw new OrderValidationException("El campo DeliveryAddress es obligatorio para tipo Delivery.");

                    deliveryZone = await _db.DeliveryZones.FindAsync(request.Delivery.DeliveryZoneId.Value);
                    if (deliveryZone == null)
                        throw new OrderNotFoundException(
                            $"Zona de delivery con Id '{request.Delivery.DeliveryZoneId}' no encontrada.");

                    if (!deliveryZone.IsActive)
                        throw new OrderValidationException(
                            $"La zona de delivery '{deliveryZone.Name}' no está activa.");

                    deliveryCost = deliveryZone.Cost; // Historical snapshot
                    deliveryZoneName = deliveryZone.Name;
                    initialStatus = OrderStatus.WaitingPayment;
                    break;

                case DeliveryType.MeetingPoint:
                    if (request.Delivery.MeetingPointId == null)
                        throw new OrderValidationException("El campo MeetingPointId es obligatorio para tipo MeetingPoint.");

                    meetingPoint = await _db.MeetingPoints.FindAsync(request.Delivery.MeetingPointId.Value);
                    if (meetingPoint == null)
                        throw new OrderNotFoundException(
                            $"Punto de encuentro con Id '{request.Delivery.MeetingPointId}' no encontrado.");

                    if (!meetingPoint.IsActive)
                        throw new OrderValidationException(
                            $"El punto de encuentro '{meetingPoint.Name}' no está activo.");

                    deliveryCost = meetingPoint.Cost; // Historical snapshot
                    meetingPointName = meetingPoint.Name;
                    initialStatus = OrderStatus.WaitingPayment;
                    break;

                case DeliveryType.NationalShipping:
                    if (string.IsNullOrWhiteSpace(request.Delivery.Department))
                        throw new OrderValidationException("El campo Department es obligatorio para envío nacional.");
                    if (string.IsNullOrWhiteSpace(request.Delivery.Province))
                        throw new OrderValidationException("El campo Province es obligatorio para envío nacional.");
                    if (string.IsNullOrWhiteSpace(request.Delivery.District))
                        throw new OrderValidationException("El campo District es obligatorio para envío nacional.");

                    deliveryCost = null; // Will be set after quoting
                    initialStatus = OrderStatus.WaitingQuote;
                    break;

                default:
                    throw new OrderValidationException($"Tipo de entrega no soportado: '{deliveryType}'.");
            }

            // --- 8. Build entities inside a transaction ---
            var supportsTransactions = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            if (supportsTransactions)
                transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // Create Customer
                var customer = new Customer
                {
                    Name = request.Customer.Name.Trim(),
                    Phone = request.Customer.Phone.Trim(),
                    Email = request.Customer.Email?.Trim()
                };
                _db.Customers.Add(customer);

                // Generate OrderCode
                var orderCode = await GenerateOrderCodeAsync();

                // Build OrderItems and calculate subtotals
                var orderItems = new List<OrderItem>();
                decimal orderSubtotal = 0;

                foreach (var item in request.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    var itemSubtotal = product.Price * item.Quantity;

                    var orderItem = new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price, // Historical price snapshot
                        Subtotal = itemSubtotal
                    };

                    orderItems.Add(orderItem);
                    orderSubtotal += itemSubtotal;
                }

                // Calculate total
                decimal? orderTotal = deliveryCost.HasValue
                    ? orderSubtotal + deliveryCost.Value
                    : null; // NationalShipping: total unknown until quoted

                // Create Order
                var order = new Order
                {
                    CustomerId = customer.Id,
                    OrderCode = orderCode,
                    DeliveryType = deliveryType,
                    DeliveryZoneId = deliveryZone?.Id,
                    MeetingPointId = meetingPoint?.Id,
                    DeliveryAddress = request.Delivery.DeliveryAddress?.Trim(),
                    Department = request.Delivery.Department?.Trim(),
                    Province = request.Delivery.Province?.Trim(),
                    District = request.Delivery.District?.Trim(),
                    Subtotal = orderSubtotal,
                    DeliveryCost = deliveryCost,
                    Total = orderTotal,
                    Status = initialStatus,
                    Items = orderItems
                };

                _db.Orders.Add(order);

                // For Delivery and MeetingPoint, create Payment
                if (deliveryType != DeliveryType.NationalShipping && orderTotal.HasValue)
                {
                    var payment = new Payment
                    {
                        OrderId = order.Id,
                        Amount = orderTotal.Value
                        // Method = Yape, Status = Pending (set by constructor)
                    };
                    _db.Payments.Add(payment);
                }

                // For NationalShipping, create a Quote with Pending status
                if (deliveryType == DeliveryType.NationalShipping)
                {
                    var quote = new Quote
                    {
                        OrderId = order.Id
                        // ShippingCost = null, Status = Pending (set by constructor)
                    };
                    _db.Quotes.Add(quote);
                }

                // Create initial status history entry
                _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = initialStatus
                });

                await _db.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                _logger.LogInformation(
                    "Pedido creado {OrderCode} tipo {DeliveryType} para cliente {CustomerId}",
                    order.OrderCode, deliveryType, customer.Id);

                // --- Trigger Notification ---
                await _notificationService.NotifyAsync(order.Id, NotificationType.OrderCreated);

                // --- 9. Build response ---
                return new CreateOrderResponse
                {
                    Id = order.Id,
                    OrderCode = order.OrderCode,
                    DeliveryType = order.DeliveryType.ToString(),
                    Subtotal = order.Subtotal,
                    DeliveryCost = order.DeliveryCost,
                    Total = order.Total,
                    Status = order.Status.ToString(),
                    CreatedAt = order.CreatedAt,
                    Items = orderItems.Select(oi =>
                    {
                        var product = products.First(p => p.Id == oi.ProductId);
                        return new CreateOrderItemResponse
                        {
                            ProductId = oi.ProductId,
                            ProductName = product.Name,
                            Quantity = oi.Quantity,
                            UnitPrice = oi.UnitPrice,
                            Subtotal = oi.Subtotal
                        };
                    }).ToList(),
                    Delivery = new CreateOrderDeliveryResponse
                    {
                        DeliveryZoneName = deliveryZoneName,
                        MeetingPointName = meetingPointName,
                        DeliveryAddress = order.DeliveryAddress,
                        Department = order.Department,
                        Province = order.Province,
                        District = order.District
                    }
                };
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        /// <summary>
        /// Generates a unique, human-readable order code in format PED-YYYY-NNNNNN.
        /// Uses the max existing sequence number for the current year to avoid gaps/collisions.
        /// </summary>
        private async Task<string> GenerateOrderCodeAsync()
        {
            var year = DateTimeOffset.UtcNow.Year;
            var prefix = $"PED-{year}-";

            // Find the highest existing order code for this year
            var lastCode = await _db.Orders
                .Where(o => o.OrderCode.StartsWith(prefix))
                .OrderByDescending(o => o.OrderCode)
                .Select(o => o.OrderCode)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastCode != null)
            {
                // Extract the numeric part after "PED-YYYY-"
                var numericPart = lastCode.Substring(prefix.Length);
                if (int.TryParse(numericPart, out var lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D6}";
        }

        public async Task<bool> AcceptQuoteAsync(Guid orderId)
        {
            var order = await _db.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                throw new OrderNotFoundException($"Pedido con Id '{orderId}' no encontrado.");

            if (order.Status != OrderStatus.QuoteReady)
                throw new OrderValidationException($"El pedido '{order.OrderCode}' no tiene una cotización lista para aceptar. Estado actual: {order.Status}");

            if (order.Total == null)
                throw new OrderValidationException("El pedido no tiene un total definido.");

            if (order.Payment != null)
                throw new OrderValidationException("El pedido ya tiene un pago generado.");

            // Create Payment
            var payment = new Payment
            {
                OrderId = order.Id,
                Amount = order.Total.Value
                // Method = Yape, Status = Pending
            };
            _db.Payments.Add(payment);

            order.Status = OrderStatus.WaitingPayment;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            // Record status history
            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.WaitingPayment,
                Comment = "Cotización aceptada por el cliente."
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation("Cotización aceptada para pedido {OrderCode}. Payment {PaymentId} generado.", order.OrderCode, payment.Id);
            return true;
        }
    }

    /// <summary>
    /// Thrown when order validation fails (invalid data, unavailable product, insufficient stock, duplicates).
    /// </summary>
    public class OrderValidationException : Exception
    {
        public OrderValidationException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when a referenced product does not exist.
    /// </summary>
    public class OrderNotFoundException : Exception
    {
        public OrderNotFoundException(string message) : base(message) { }
    }
}
