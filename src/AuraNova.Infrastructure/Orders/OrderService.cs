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
    public class OrderService(
        AppDbContext db,
        INotificationService notificationService,
        ILogger<OrderService> logger,
        IOrderStatusTransitionService transitionService) : IOrderService
    {
        private readonly AppDbContext _db = db;
        private readonly INotificationService _notificationService = notificationService;
        private readonly ILogger<OrderService> _logger = logger;
        private readonly IOrderStatusTransitionService _transitionService = transitionService;


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

                // Deduct stock to reserve the items
                product.Stock -= item.Quantity;
            }

            // --- 7. Validate delivery info ---
            if (request.Delivery == null)
                throw new OrderValidationException("La información de entrega es obligatoria.");

            if (!Enum.TryParse<DeliveryType>(request.Delivery.Type, ignoreCase: true, out var deliveryType))
                throw new OrderValidationException(
                    $"Tipo de entrega inválido: '{request.Delivery.Type}'. Valores válidos: Delivery, MeetingPoint, NationalShipping.");

            // Delivery-type-specific validation and cost resolution
            decimal? deliveryCost;
            DeliveryZone? deliveryZone = null;
            MeetingPoint? meetingPoint = null;
            OrderStatus initialStatus;
            string? deliveryZoneName = null;
            string? meetingPointName = null;

            switch (deliveryType)
            {
                case DeliveryType.Delivery:
                    if (request.Delivery.DeliveryZoneId == null)
                        throw new OrderValidationException(
                            "El campo DeliveryZoneId es obligatorio para tipo Delivery.");

                    if (string.IsNullOrWhiteSpace(request.Delivery.DeliveryAddress))
                        throw new OrderValidationException(
                            "El campo DeliveryAddress es obligatorio para tipo Delivery.");

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
                        throw new OrderValidationException(
                            "El campo MeetingPointId es obligatorio para tipo MeetingPoint.");

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

                    deliveryCost = null; // Costo asumido por el cliente en destino
                    initialStatus = OrderStatus.WaitingPayment;
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
                        Subtotal = itemSubtotal,
                        SelectedPrimaryColor = item.SelectedPrimaryColor,
                        SelectedSecondaryColor = item.SelectedSecondaryColor,
                        SelectedFlowerType = item.SelectedFlowerType,
                        SelectedFlowerColor = item.SelectedFlowerColor,
                        HasLights = item.HasLights,
                        HasButterfly = item.HasButterfly,
                        HasPhraseCard = item.HasPhraseCard,
                        PhraseText = item.PhraseText,
                        PhraseFont = item.PhraseFont
                    };

                    orderItems.Add(orderItem);
                    orderSubtotal += itemSubtotal;
                }

                // Calculate total
                decimal? orderTotal = deliveryCost.HasValue
                    ? orderSubtotal + deliveryCost.Value
                    : orderSubtotal; // Para NationalShipping, el total es solo el subtotal de productos

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

                // Create Payment for all standard orders
                if (orderTotal.HasValue)
                {
                    var payment = new Payment
                    {
                        OrderId = order.Id,
                        Amount = orderTotal.Value
                        // Method = Yape, Status = Pending (set by constructor)
                    };
                    _db.Payments.Add(payment);
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
                    TrackingToken = order.TrackingToken,
                    CreatedAt = order.CreatedAt,
                    Items =
                    [
                        .. orderItems.Select(oi =>
                        {
                            var product = products.First(p => p.Id == oi.ProductId);
                            return new CreateOrderItemResponse
                            {
                                ProductId = oi.ProductId,
                                ProductName = product.Name,
                                Quantity = oi.Quantity,
                                UnitPrice = oi.UnitPrice,
                                Subtotal = oi.Subtotal,
                                SelectedPrimaryColor = oi.SelectedPrimaryColor,
                                SelectedSecondaryColor = oi.SelectedSecondaryColor,
                                SelectedFlowerType = oi.SelectedFlowerType,
                                SelectedFlowerColor = oi.SelectedFlowerColor,
                                HasLights = oi.HasLights,
                                HasButterfly = oi.HasButterfly,
                                HasPhraseCard = oi.HasPhraseCard,
                                PhraseText = oi.PhraseText,
                                PhraseFont = oi.PhraseFont
                            };
                        })
                    ],
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
        public async Task<CreateOrderResponse> CreateCustomAsync(CreateCustomOrderRequest request)
        {
            var supportsTransactions = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            if (supportsTransactions)
                transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // --- 1. Identify or Create Customer ---
                var customer = await _db.Customers
                    .FirstOrDefaultAsync(c => c.Phone == request.Customer.Phone.Trim());

                if (customer == null)
                {
                    customer = new Customer
                    {
                        Phone = request.Customer.Phone.Trim(),
                        Name = request.Customer.Name.Trim(),
                        Email = request.Customer.Email?.Trim()
                    };
                    _db.Customers.Add(customer);
                }
                else
                {
                    customer.Name = request.Customer.Name.Trim();
                    if (!string.IsNullOrWhiteSpace(request.Customer.Email))
                        customer.Email = request.Customer.Email.Trim();
                }

                // --- 2. Generate Order Code ---
                var orderCode = await GenerateOrderCodeAsync();

                // --- 3. Delivery Details ---
                if (!Enum.TryParse<DeliveryType>(request.Delivery.Type, out var deliveryType))
                    throw new OrderValidationException($"Tipo de entrega inválido: {request.Delivery.Type}");

                DeliveryZone? deliveryZone = null;
                MeetingPoint? meetingPoint = null;
                decimal? deliveryCost = null;
                string? deliveryZoneName = null;
                string? meetingPointName = null;

                switch (deliveryType)
                {
                    case DeliveryType.Delivery:
                        if (request.Delivery.DeliveryZoneId == null)
                            throw new OrderValidationException(
                                "El campo DeliveryZoneId es obligatorio para tipo Delivery.");
                        deliveryZone = await _db.DeliveryZones.FindAsync(request.Delivery.DeliveryZoneId.Value);
                        if (deliveryZone == null)
                            throw new OrderNotFoundException(
                                $"Zona de delivery con Id '{request.Delivery.DeliveryZoneId}' no encontrada.");
                        deliveryCost = deliveryZone.Cost;
                        deliveryZoneName = deliveryZone.Name;
                        break;
                    case DeliveryType.MeetingPoint:
                        if (request.Delivery.MeetingPointId == null)
                            throw new OrderValidationException(
                                "El campo MeetingPointId es obligatorio para tipo MeetingPoint.");
                        meetingPoint = await _db.MeetingPoints.FindAsync(request.Delivery.MeetingPointId.Value);
                        if (meetingPoint == null)
                            throw new OrderNotFoundException(
                                $"Punto de encuentro con Id '{request.Delivery.MeetingPointId}' no encontrado.");
                        deliveryCost = meetingPoint.Cost;
                        meetingPointName = meetingPoint.Name;
                        break;
                    case DeliveryType.NationalShipping:
                        deliveryCost = null;
                        break;
                }

                // --- 4. Create Order ---
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
                    Subtotal = 0, // Custom order base price is 0 until quoted
                    DeliveryCost = deliveryCost,
                    Total = null, // Custom orders always need quoting for total
                    Status = OrderStatus.WaitingQuote,
                    IsCustomOrder = true,
                    ReferenceImageUrl = request.ReferenceImageUrl,
                    CustomizationNotes = request.CustomizationNotes,
                    Items = new List<OrderItem>()
                };

                _db.Orders.Add(order);

                // --- 5. Always Create Quote ---
                var quote = new Quote
                {
                    OrderId = order.Id
                };
                _db.Quotes.Add(quote);

                _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = OrderStatus.WaitingQuote,
                    Comment = "Pedido personalizado ingresado a cotización."
                });

                await _db.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                _logger.LogInformation("Pedido Personalizado creado {OrderCode} para cliente {CustomerId}",
                    order.OrderCode, customer.Id);

                await _notificationService.NotifyAsync(order.Id, NotificationType.OrderCreated);

                return new CreateOrderResponse
                {
                    Id = order.Id,
                    OrderCode = order.OrderCode,
                    DeliveryType = order.DeliveryType.ToString(),
                    Subtotal = order.Subtotal,
                    DeliveryCost = order.DeliveryCost,
                    Total = order.Total,
                    Status = order.Status.ToString(),
                    TrackingToken = order.TrackingToken,
                    CreatedAt = order.CreatedAt,
                    Items = [],
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
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }

        private async Task<string> GenerateOrderCodeAsync()
        {
            var prefix = "PED-";

            // Find the highest existing order code for this prefix
            var lastCode = await _db.Orders
                .Where(o => o.OrderCode.StartsWith(prefix))
                .OrderByDescending(o => o.OrderCode)
                .Select(o => o.OrderCode)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastCode != null)
            {
                // Extract the numeric part after "PED-"
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
                throw new OrderValidationException(
                    $"El pedido '{order.OrderCode}' no tiene una cotización lista para aceptar. Estado actual: {order.Status}");

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

            _logger.LogInformation("Cotización aceptada para pedido {OrderCode}. Payment {PaymentId} generado.",
                order.OrderCode, payment.Id);
            return true;
        }

        public async Task StartPreparationAsync(Guid id)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) throw new OrderNotFoundException("Pedido no encontrado.");

            if (!_transitionService.IsTransitionAllowed(order.Status, OrderStatus.Preparing, order.DeliveryType))
                throw new OrderValidationException(
                    $"No se puede iniciar preparación desde el estado actual {order.Status}.");

            order.Status = OrderStatus.Preparing;
            order.StartedAt = DateTimeOffset.UtcNow;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.Preparing,
                Comment = "Elaboración iniciada"
            });

            await _db.SaveChangesAsync();
        }

        public async Task SetEstimatedReadyDateAsync(Guid id, DateTimeOffset estimatedDate)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) throw new OrderNotFoundException("Pedido no encontrado.");

            order.EstimatedReadyAt = estimatedDate;
            await _db.SaveChangesAsync();
        }

        public async Task MarkAsReadyAsync(Guid id)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) throw new OrderNotFoundException("Pedido no encontrado.");

            if (!_transitionService.IsTransitionAllowed(order.Status, OrderStatus.Ready, order.DeliveryType))
                throw new OrderValidationException(
                    $"No se puede marcar como listo desde el estado actual {order.Status}.");

            order.Status = OrderStatus.Ready;
            order.ReadyAt = DateTimeOffset.UtcNow;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.Ready,
                Comment = "Pedido listo"
            });

            await _db.SaveChangesAsync();
        }

        public async Task DeliverToAgencyAsync(Guid id, string provider, string trackingCode, string? proofUrl)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) throw new OrderNotFoundException("Pedido no encontrado.");

            if (order.DeliveryType != DeliveryType.NationalShipping)
                throw new OrderValidationException("Solo los envíos nacionales pueden entregarse a agencia.");

            if (!_transitionService.IsTransitionAllowed(order.Status, OrderStatus.DeliveredToAgency,
                    order.DeliveryType))
                throw new OrderValidationException(
                    $"No se puede registrar entrega en agencia desde el estado actual {order.Status}.");

            if (string.IsNullOrWhiteSpace(provider))
                throw new OrderValidationException("El proveedor de envío es obligatorio.");

            order.Status = OrderStatus.DeliveredToAgency;
            order.DeliveredToAgencyAt = DateTimeOffset.UtcNow;
            order.ShippingProvider = provider;
            order.ShippingTrackingCode = trackingCode;
            order.ShippingProofUrl = proofUrl;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.DeliveredToAgency,
                Comment = $"Entregado a {provider} - Tracking: {trackingCode}"
            });

            await _db.SaveChangesAsync();
        }

        public async Task CancelAsync(Guid id, string reason)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) throw new OrderNotFoundException("Pedido no encontrado.");

            if (!_transitionService.IsTransitionAllowed(order.Status, OrderStatus.Cancelled, order.DeliveryType))
                throw new OrderValidationException($"No se puede cancelar desde el estado {order.Status}.");

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.Cancelled,
                Comment = $"Cancelado: {reason}"
            });

            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Thrown when order validation fails (invalid data, unavailable product, insufficient stock, duplicates).
    /// </summary>
    public class OrderValidationException(string message) : Exception(message)
    {
    }

    /// <summary>
    /// Thrown when a referenced product does not exist.
    /// </summary>
    public class OrderNotFoundException(string message) : Exception(message)
    {
    }
}
