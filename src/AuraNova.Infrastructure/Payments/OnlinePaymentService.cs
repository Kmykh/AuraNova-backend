using System.Text.RegularExpressions;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.OnlinePayments.DTOs;
using AuraNova.Application.OnlinePayments.Interfaces;
using AuraNova.Application.Orders;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Payments
{
    /// <summary>
    /// Cobros en la página con Mercado Pago (tarjeta o Yape). Regla del negocio: el primer pago es el 50% o el total;
    /// con el 50% el pedido ya pasa a "pago confirmado" y queda un saldo que se paga después en la misma página.
    /// </summary>
    public class OnlinePaymentService(
        AppDbContext db,
        IMercadoPagoGateway gateway,
        INotificationService notificationService,
        ILogger<OnlinePaymentService> logger,
        IStorageUrlResolver? storageUrls = null,
        ICurrentUserService? currentUser = null,
        TimeSpan? verifyDelay = null) : IOnlinePaymentService
    {
        // Un cobro "en proceso" bloquea otro intento durante este tiempo (evita pagar dos veces)
        private static readonly TimeSpan PendingLock = TimeSpan.FromMinutes(15);
        // Intento sin order en Mercado Pago (no respondió a tiempo o era un envío repetido): se espera poco
        private static readonly TimeSpan UnknownOutcomeLock = TimeSpan.FromMinutes(3);
        private static readonly string[] UnresolvedStatuses = ["created", "processing", "action_required"];
        private static readonly Regex MethodIdPattern = new("^[a-z_]{2,30}$", RegexOptions.Compiled);

        public static decimal DepositFor(decimal total) => Math.Round(total / 2m, 2, MidpointRounding.AwayFromZero);

        public async Task<OnlinePaymentResult> PayAsync(Guid orderId, CreateOnlinePaymentRequest request, CancellationToken cancellationToken = default)
        {
            var order = await db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items!).ThenInclude(i => i.Product)
                .Include(o => o.Payment!).ThenInclude(p => p.Transactions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                ?? throw new OnlinePaymentException("No encontramos este pedido.");

            if (order.Total is not > 0)
                throw new OnlinePaymentException("Este pedido todavía no tiene un precio confirmado.");
            if (order.Status is OrderStatus.Cancelled)
                throw new OnlinePaymentException("Este pedido fue cancelado.");

            // Pedidos que quedaron sin registro de pago con el flujo anterior
            var payment = order.Payment;
            if (payment == null)
            {
                payment = new Payment { OrderId = order.Id, Amount = order.Total.Value };
                db.Payments.Add(payment);
                order.Payment = payment;
            }

            var balance = payment.Amount - payment.AmountPaid;
            if (balance <= 0)
                throw new OnlinePaymentException("Este pedido ya está pagado.");

            var isFirstPayment = payment.AmountPaid <= 0;
            if (isFirstPayment && order.Status != OrderStatus.WaitingPayment)
                throw new OnlinePaymentException(order.Status is OrderStatus.WaitingQuote or OrderStatus.QuoteReady
                    ? "Primero acepta la cotización para poder pagar."
                    : "Este pedido no está esperando un pago.");

            // El servidor decide cuánto se cobra: adelanto, total o saldo
            var isDeposit = isFirstPayment && !request.PayFull;
            var amount = isFirstPayment ? (isDeposit ? DepositFor(payment.Amount) : payment.Amount) : balance;

            // Otro intento de este pedido se está enviando ahora mismo (doble toque en "Pagar"): no se manda otro;
            // la página espera la confirmación del primero
            var now = DateTimeOffset.UtcNow;
            if (payment.Transactions?.Any(t => t.ApprovedAt == null && t.Status == "created" && t.CreatedAt > now.AddMinutes(-2)) == true)
                return InProgress(order, payment, amount);

            if (payment.Transactions?.Any(t => t.ApprovedAt == null && t.Status is "processing" or "action_required" &&
                                               t.CreatedAt > now - (t.ExternalOrderId == null ? UnknownOutcomeLock : PendingLock)) == true)
                throw new OnlinePaymentException("Tienes un pago en proceso. Espera unos minutos antes de intentar de nuevo.");

            var (methodId, methodType) = ResolveMethod(request);
            var email = (request.PayerEmail ?? order.Customer?.Email)?.Trim();
            if (string.IsNullOrWhiteSpace(email))
                throw new OnlinePaymentException("Necesitamos un correo electrónico para el pago.");

            string? phone = null;
            if (methodId == "yape")
            {
                phone = new string((request.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
                if (phone.Length != 9)
                    throw new OnlinePaymentException("Ingresa el celular de 9 dígitos asociado a tu Yape.");
            }

            // Documento: el que escribió en el formulario de la tarjeta o, si no, el DNI que dio al pedir
            string? documentNumber = new string((request.IdentificationNumber ?? MercadoPagoOrderData.PayerDocument(order) ?? string.Empty)
                .Where(char.IsLetterOrDigit).ToArray());
            if (documentNumber.Length == 0) documentNumber = null;

            var (firstName, lastName) = MercadoPagoOrderData.SplitName(order.Customer?.Name);
            var history = order.Customer == null ? null : await BuyerHistoryAsync(order, cancellationToken);

            var transaction = new PaymentTransaction
            {
                PaymentId = payment.Id,
                OrderId = order.Id,
                MethodId = methodId,
                MethodType = methodType,
                Amount = amount,
                IsDeposit = isDeposit,
                Status = "created"
            };
            db.PaymentTransactions.Add(transaction);
            await db.SaveChangesAsync(cancellationToken);

            GatewayOrder result;
            try
            {
                result = await gateway.CreateOrderAsync(new GatewayCreateOrder(
                    ExternalReference: transaction.Id.ToString("N"),
                    Amount: amount,
                    Description: $"Pedido {order.OrderCode} - Aura Nova" + (isDeposit ? " (adelanto 50%)" : isFirstPayment ? "" : " (saldo)"),
                    PayerEmail: email,
                    // La API exige el número si se envía el documento: solo va cuando lo tenemos
                    IdentificationType: documentNumber == null ? null
                        : request.IdentificationType?.Trim() is { Length: > 0 } type ? type
                        : documentNumber.Length == 8 && documentNumber.All(char.IsDigit) ? "DNI" : "CE",
                    IdentificationNumber: documentNumber,
                    PhoneNumber: phone ?? MercadoPagoOrderData.Phone(order.Customer?.Phone),
                    PaymentMethodId: methodId,
                    PaymentMethodType: methodType,
                    Token: request.Token.Trim(),
                    Installments: request.Installments,
                    // Por pedido y token: un doble envío no cobra dos veces y otro pedido nunca comparte la clave
                    IdempotencyKey: MercadoPagoGateway.IdempotencyKeyFor($"{order.Id:N}:{request.Token.Trim()}"),
                    PayerFirstName: firstName,
                    PayerLastName: lastName,
                    PayerAddress: MercadoPagoOrderData.PayerAddress(order),
                    Items: MercadoPagoOrderData.Items(order, amount, path => storageUrls?.ToPublicUrl(path) ?? path),
                    ShipmentAddress: MercadoPagoOrderData.ShipmentAddress(order),
                    History: history,
                    DeviceId: request.DeviceId?.Trim()), cancellationToken);
            }
            catch (MercadoPagoInProgressException ex)
            {
                // Mercado Pago suele terminar en segundos aunque haya respondido 423 o no a tiempo:
                // se le pregunta de inmediato por la referencia de este intento antes de responder "en proceso"
                var found = await FindSoonAsync(transaction, cancellationToken);
                if (found == null)
                {
                    // Sigue sin saberse: queda en proceso y la conciliación lo confirma (o lo descarta)
                    transaction.Status = "processing";
                    transaction.StatusDetail = Truncate(ex.Detail ?? ex.Message, 100);
                    transaction.UpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    return InProgress(order, payment, amount);
                }
                logger.LogInformation("Cobro {TransactionId} confirmado al consultar a Mercado Pago tras {Detail}", transaction.Id, ex.Detail);
                result = found;
            }
            catch (MercadoPagoGatewayException ex)
            {
                transaction.Status = "error";
                // En el panel queda el motivo real de Mercado Pago; al cliente, el mensaje amable
                transaction.StatusDetail = Truncate(ex.Detail ?? ex.Message, 100);
                transaction.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                throw new OnlinePaymentException(ex.Message);
            }

            // Reenvío del mismo pago (mismo token = misma order en Mercado Pago): se usa el cobro ya registrado
            var existing = result.Id == null ? null : await db.PaymentTransactions
                .FirstOrDefaultAsync(t => t.ExternalOrderId == result.Id && t.Id != transaction.Id, cancellationToken);
            if (existing != null)
            {
                db.PaymentTransactions.Remove(transaction);
                transaction = existing;
            }

            await ApplyAsync(order, payment, transaction, result, cancellationToken);

            // Suma desde la BD: si el aviso de Mercado Pago aún está guardando el pago, el monto igual sale correcto
            var paid = Math.Min(await NetPaidAsync(payment.Id, cancellationToken), payment.Amount);
            var due = payment.Amount - paid;
            return new OnlinePaymentResult
            {
                Status = transaction.ApprovedAt != null ? "approved" : result.IsFailed ? "rejected" : "pending",
                Message = transaction.ApprovedAt != null
                    ? due > 0
                        ? $"¡Pago recibido! Pagaste el adelanto. Te falta {Money(due)}, que puedes pagar aquí mismo cuando quieras."
                        : "¡Pago recibido! Tu pedido está pagado."
                    : result.IsFailed
                        ? RejectionMessage(result.Detail)
                        : "Tu pago está en proceso. Te avisaremos apenas Mercado Pago lo confirme.",
                AmountCharged = amount,
                AmountPaid = paid,
                BalanceDue = due,
                OrderStatus = order.Status.ToString()
            };
        }

        public async Task HandleOrderNotificationAsync(string mercadoPagoOrderId, CancellationToken cancellationToken = default)
        {
            // Nunca se confía en el contenido del aviso: se consulta la order a Mercado Pago con nuestro token
            var result = await gateway.GetOrderAsync(mercadoPagoOrderId, cancellationToken);
            if (result == null)
            {
                logger.LogWarning("Webhook de Mercado Pago para una order inexistente: {OrderId}", mercadoPagoOrderId);
                return;
            }

            var transaction = await db.PaymentTransactions
                .FirstOrDefaultAsync(t => t.ExternalOrderId == result.Id, cancellationToken);
            if (transaction == null && Guid.TryParseExact(result.ExternalReference, "N", out var transactionId))
                transaction = await db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                logger.LogWarning("Webhook de Mercado Pago sin cobro asociado: {OrderId}", mercadoPagoOrderId);
                return;
            }

            var order = await db.Orders
                .Include(o => o.Payment!).ThenInclude(p => p.Transactions)
                .FirstAsync(o => o.Id == transaction.OrderId, cancellationToken);

            await ApplyAsync(order, order.Payment!, transaction, result, cancellationToken);
        }

        public async Task<RefundOnlinePaymentResult> RefundAsync(Guid orderId, Guid transactionId, RefundOnlinePaymentRequest request, CancellationToken cancellationToken = default)
        {
            var order = await db.Orders
                .Include(o => o.Payment!).ThenInclude(p => p.Transactions)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                ?? throw new OnlinePaymentException("No encontramos este pedido.");
            var transaction = order.Payment?.Transactions?.FirstOrDefault(t => t.Id == transactionId)
                ?? throw new OnlinePaymentException("No encontramos ese cobro en el pedido.");

            if (transaction.ApprovedAt == null || transaction.ExternalOrderId == null)
                throw new OnlinePaymentException("Solo se pueden devolver cobros aprobados por Mercado Pago.");

            var refundable = transaction.Amount - transaction.RefundedAmount;
            if (refundable <= 0)
                throw new OnlinePaymentException("Este cobro ya fue devuelto por completo.");

            var amount = Math.Round(request.Amount ?? refundable, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0 || amount > refundable)
                throw new OnlinePaymentException($"Puedes devolver como máximo {Money(refundable)} de este cobro.");

            var isPartial = amount < refundable;
            if (isPartial && transaction.ExternalPaymentId == null)
                throw new OnlinePaymentException("Este cobro solo se puede devolver completo.");

            try
            {
                // Misma clave para el mismo pedido de devolución: un doble clic no devuelve dos veces
                var key = MercadoPagoGateway.IdempotencyKeyFor($"refund:{transaction.Id}:{transaction.RefundedAmount}:{amount}");
                await gateway.RefundAsync(transaction.ExternalOrderId, isPartial ? transaction.ExternalPaymentId : null,
                    isPartial ? amount : null, key, cancellationToken);
            }
            catch (MercadoPagoGatewayException ex)
            {
                throw new OnlinePaymentException(ex.Message);
            }

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Motivo: {request.Reason.Trim()}";
            await RecordRefundAsync(order, order.Payment!, transaction, amount,
                $"Devolución de {Money(amount)} ({MethodName(transaction)}) por Mercado Pago.{reason}", cancellationToken);

            var paid = order.Payment!.AmountPaid;
            return new RefundOnlinePaymentResult
            {
                Refunded = amount,
                AmountPaid = paid,
                BalanceDue = order.Payment.Amount - paid,
                Message = $"Devolvimos {Money(amount)}. El cliente lo verá en su {MethodName(transaction)} según los plazos de su banco."
            };
        }

        public async Task CancelPendingAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            // Mercado Pago solo permite anular orders que esperan una acción del cliente (o recién creadas)
            var pending = await db.PaymentTransactions
                .Where(t => t.OrderId == orderId && t.ApprovedAt == null && t.ExternalOrderId != null &&
                            (t.Status == "action_required" || t.Status == "created"))
                .ToListAsync(cancellationToken);

            foreach (var transaction in pending)
            {
                try
                {
                    var result = await gateway.CancelAsync(transaction.ExternalOrderId!, cancellationToken);
                    transaction.Status = Truncate(result.Status ?? "canceled", 40)!;
                    transaction.StatusDetail = Truncate(result.Detail, 100);
                    transaction.UpdatedAt = DateTimeOffset.UtcNow;
                }
                catch (MercadoPagoGatewayException ex)
                {
                    logger.LogWarning(ex, "No se pudo anular en Mercado Pago la order {ExternalOrderId}", transaction.ExternalOrderId);
                }
            }

            if (pending.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Consulta un cobro en Mercado Pago y aplica lo que diga. Devuelve true si cambió su estado.</summary>
        private async Task<bool> ReconcileAsync(PaymentTransaction transaction, DateTimeOffset now, CancellationToken cancellationToken)
        {
            try
            {
                var before = transaction.Status;
                var result = transaction.ExternalOrderId != null
                    ? await gateway.GetOrderAsync(transaction.ExternalOrderId, cancellationToken)
                    : await gateway.FindByExternalReferenceAsync(transaction.Id.ToString("N"), cancellationToken);

                if (result == null)
                {
                    if (transaction.ExternalOrderId == null && transaction.CreatedAt < now.AddMinutes(-30))
                    {
                        transaction.Status = "error";
                        transaction.StatusDetail = "Mercado Pago no registró este intento.";
                        transaction.UpdatedAt = now;
                        await db.SaveChangesAsync(cancellationToken);
                        return true;
                    }
                    return false;
                }

                var order = await db.Orders
                    .Include(o => o.Payment!).ThenInclude(p => p.Transactions)
                    .FirstAsync(o => o.Id == transaction.OrderId, cancellationToken);
                await ApplyAsync(order, order.Payment!, transaction, result, cancellationToken);
                return transaction.Status != before;
            }
            catch (MercadoPagoGatewayException ex)
            {
                logger.LogWarning(ex, "Conciliación: no se pudo consultar el cobro {TransactionId}", transaction.Id);
                return false;
            }
        }

        /// <summary>
        /// "Verificar con Mercado Pago" desde el panel: revisa ya los cobros sin aprobar del pedido (de los últimos 3 días),
        /// sin esperar a la conciliación automática. Sirve cuando Mercado Pago cobró pero la respuesta no llegó.
        /// </summary>
        public async Task<int> ReconcileOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var candidates = await db.PaymentTransactions
                .Where(t => t.OrderId == orderId && t.ApprovedAt == null && t.CreatedAt > now.AddDays(-3) &&
                            (UnresolvedStatuses.Contains(t.Status) || t.Status == "error"))
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var changed = 0;
            foreach (var transaction in candidates)
                if (await ReconcileAsync(transaction, now, cancellationToken))
                    changed++;
            return changed;
        }

        public async Task<int> ReconcilePendingAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var settled = now.AddMinutes(-2);
            var candidates = await db.PaymentTransactions
                .Where(t => t.ApprovedAt == null && t.CreatedAt < settled && t.CreatedAt > now.AddDays(-3) &&
                            (UnresolvedStatuses.Contains(t.Status) ||
                             // Sin respuesta al crearla (corte de red): puede que Mercado Pago sí la haya cobrado
                             (t.Status == "error" && t.ExternalOrderId == null && t.CreatedAt > now.AddMinutes(-30))))
                .OrderBy(t => t.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            var changed = 0;
            foreach (var transaction in candidates)
                if (await ReconcileAsync(transaction, now, cancellationToken))
                    changed++;

            if (changed > 0)
                logger.LogInformation("Conciliación de Mercado Pago: {Changed} cobro(s) actualizados", changed);

            // Los avisos recibidos se guardan 30 días
            if (db.Database.IsRelational())
                await db.PaymentWebhookEvents.Where(e => e.ReceivedAt < now.AddDays(-30)).ExecuteDeleteAsync(cancellationToken);

            return changed;
        }

        public async Task LogWebhookAsync(string? topic, string? resourceId, string result, string? detail, CancellationToken cancellationToken = default)
        {
            db.PaymentWebhookEvents.Add(new PaymentWebhookEvent
            {
                Topic = Truncate(topic, 40),
                ResourceId = Truncate(resourceId, 100),
                Result = result,
                Detail = Truncate(detail, 250)
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PaymentWebhookEventResponse>> GetWebhookEventsAsync(int take, CancellationToken cancellationToken = default) =>
            await db.PaymentWebhookEvents
                .OrderByDescending(e => e.ReceivedAt)
                .Take(Math.Clamp(take, 1, 200))
                .Select(e => new PaymentWebhookEventResponse
                {
                    ReceivedAt = e.ReceivedAt,
                    Topic = e.Topic,
                    ResourceId = e.ResourceId,
                    Result = e.Result,
                    Detail = e.Detail
                })
                .ToListAsync(cancellationToken);

        public async Task<OnlinePaymentReport> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            var rows = await db.PaymentTransactions
                .Where(t => t.CreatedAt >= from && t.CreatedAt < to)
                .Join(db.Orders, t => t.OrderId, o => o.Id, (t, o) => new { Transaction = t, o.OrderCode, CustomerName = o.Customer!.Name })
                .OrderByDescending(x => x.Transaction.CreatedAt)
                .Take(2000)
                .Select(x => new OnlinePaymentReportRow
                {
                    Id = x.Transaction.Id,
                    OrderId = x.Transaction.OrderId,
                    OrderCode = x.OrderCode,
                    CustomerName = x.CustomerName,
                    MethodId = x.Transaction.MethodId,
                    MethodType = x.Transaction.MethodType,
                    Amount = x.Transaction.Amount,
                    RefundedAmount = x.Transaction.RefundedAmount,
                    IsDeposit = x.Transaction.IsDeposit,
                    Status = x.Transaction.Status,
                    StatusDetail = x.Transaction.StatusDetail,
                    Approved = x.Transaction.ApprovedAt != null,
                    ExternalOrderId = x.Transaction.ExternalOrderId,
                    CreatedAt = x.Transaction.CreatedAt,
                    ApprovedAt = x.Transaction.ApprovedAt
                })
                .ToListAsync(cancellationToken);

            var collected = rows.Where(r => r.Approved).Sum(r => r.Amount);
            var refunded = rows.Sum(r => r.RefundedAmount);
            return new OnlinePaymentReport
            {
                From = from,
                To = to,
                Collected = collected,
                Refunded = refunded,
                Net = collected - refunded,
                ApprovedCount = rows.Count(r => r.Approved),
                PendingCount = rows.Count(r => !r.Approved && UnresolvedStatuses.Contains(r.Status)),
                RejectedCount = rows.Count(r => !r.Approved && !UnresolvedStatuses.Contains(r.Status)),
                Rows = rows
            };
        }

        /// <summary>Guarda el resultado de Mercado Pago y, si se aprobó, suma el pago al pedido una sola vez.</summary>
        private async Task ApplyAsync(Order order, Payment payment, PaymentTransaction transaction, GatewayOrder result, CancellationToken cancellationToken)
        {
            transaction.ExternalOrderId ??= result.Id;
            transaction.ExternalPaymentId ??= result.Payments.FirstOrDefault()?.Id;
            transaction.Status = Truncate(result.Status ?? "unknown", 40)!;
            transaction.StatusDetail = Truncate(result.Detail, 100);
            transaction.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            // Devuelto desde el panel de Mercado Pago (no desde el nuestro): se descuenta igual
            if (result.Status == "refunded" && transaction.ApprovedAt != null && transaction.RefundedAmount < transaction.Amount)
            {
                await RecordRefundAsync(order, payment, transaction, transaction.Amount - transaction.RefundedAmount,
                    "Devolución hecha desde la cuenta de Mercado Pago.", cancellationToken);
                return;
            }

            if (!result.IsApproved || transaction.ApprovedAt != null)
                return;

            // La respuesta del pago y el webhook pueden llegar a la vez: solo el primero que marca el cobro lo suma
            var approvedAt = DateTimeOffset.UtcNow;
            if (db.Database.IsRelational())
            {
                var claimed = await db.PaymentTransactions
                    .Where(t => t.Id == transaction.Id && t.ApprovedAt == null)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.ApprovedAt, approvedAt), cancellationToken) > 0;
                if (!claimed)
                {
                    // El aviso de Mercado Pago (webhook) llegó antes que la respuesta y ya registró este pago:
                    // se toma lo guardado para responderle al cliente "aprobado" y no "en proceso"
                    await db.Entry(transaction).ReloadAsync(cancellationToken);
                    await db.Entry(payment).ReloadAsync(cancellationToken);
                    await db.Entry(order).ReloadAsync(cancellationToken);
                    return;
                }

                transaction.ApprovedAt = approvedAt;
                db.Entry(transaction).Property(t => t.ApprovedAt).IsModified = false;
            }
            else
            {
                transaction.ApprovedAt = approvedAt;
                await db.SaveChangesAsync(cancellationToken);
            }

            // Se aprobó cuando el pedido ya estaba cancelado (p. ej. un pago en revisión): se devuelve solo
            if (order.Status == OrderStatus.Cancelled)
            {
                await RefundCancelledOrderPaymentAsync(order, payment, transaction, cancellationToken);
                return;
            }

            var wasFirstPayment = payment.AmountPaid <= 0;
            payment.AmountPaid = Math.Min(await NetPaidAsync(payment.Id, cancellationToken), payment.Amount);
            payment.Method = PaymentMethod.MercadoPago;
            payment.Status = PaymentStatus.Confirmed;
            payment.VerifiedAt ??= DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;

            var due = payment.Amount - payment.AmountPaid;
            var method = MethodName(transaction);
            var comment = due > 0
                ? $"Adelanto pagado con {method} (Mercado Pago): {Money(transaction.Amount)}. Saldo pendiente: {Money(due)}."
                : wasFirstPayment
                    ? $"Pago total con {method} (Mercado Pago): {Money(transaction.Amount)}."
                    : $"Saldo pagado con {method} (Mercado Pago): {Money(transaction.Amount)}. Pedido pagado por completo.";

            var confirmsOrder = order.Status == OrderStatus.WaitingPayment;
            if (confirmsOrder)
            {
                order.Status = OrderStatus.PaymentConfirmed;
                order.UpdatedAt = DateTimeOffset.UtcNow;
            }

            db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = order.Status,
                Comment = comment
            });

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Pago Mercado Pago {ExternalOrderId} aprobado para {OrderCode}: {Amount} (saldo {Due})",
                transaction.ExternalOrderId, order.OrderCode, transaction.Amount, due);

            if (confirmsOrder)
                await notificationService.NotifyAsync(order.Id, NotificationType.PaymentConfirmed);
            else if (!wasFirstPayment && due <= 0)
                await notificationService.NotifyAsync(order.Id, NotificationType.BalancePaid);
        }

        /// <summary>Lo cobrado y aprobado menos lo devuelto.</summary>
        private Task<decimal> NetPaidAsync(Guid paymentId, CancellationToken cancellationToken) =>
            db.PaymentTransactions
                .Where(t => t.PaymentId == paymentId && t.ApprovedAt != null)
                .SumAsync(t => t.Amount - t.RefundedAmount, cancellationToken);

        /// <summary>Registra una devolución, recalcula lo pagado y lo deja en el historial del pedido.</summary>
        private async Task RecordRefundAsync(Order order, Payment payment, PaymentTransaction transaction, decimal amount, string comment, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            transaction.RefundedAmount += amount;
            transaction.RefundedAt = now;
            transaction.Status = transaction.RefundedAmount >= transaction.Amount ? "refunded" : "partially_refunded";
            transaction.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);

            payment.AmountPaid = Math.Max(0, Math.Min(await NetPaidAsync(payment.Id, cancellationToken), payment.Amount));
            payment.UpdatedAt = now;

            var due = payment.Amount - payment.AmountPaid;
            db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = order.Status,
                Comment = order.Status == OrderStatus.Cancelled || due <= 0 ? comment : $"{comment} Ahora falta pagar {Money(due)}.",
                AdminUserId = currentUser?.UserId,
                AdminName = currentUser?.Name
            });
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Devolución de {Amount} en {OrderCode} (order {ExternalOrderId})", amount, order.OrderCode, transaction.ExternalOrderId);
        }

        private async Task RefundCancelledOrderPaymentAsync(Order order, Payment payment, PaymentTransaction transaction, CancellationToken cancellationToken)
        {
            try
            {
                await gateway.RefundAsync(transaction.ExternalOrderId!, null, null,
                    MercadoPagoGateway.IdempotencyKeyFor($"auto-refund:{transaction.Id}"), cancellationToken);
                await RecordRefundAsync(order, payment, transaction, transaction.Amount,
                    $"Se aprobó un pago de {Money(transaction.Amount)} con el pedido ya cancelado: se devolvió automáticamente.", cancellationToken);
            }
            catch (MercadoPagoGatewayException ex)
            {
                logger.LogError(ex, "No se pudo devolver el pago aprobado del pedido cancelado {OrderCode}", order.OrderCode);
                db.Set<OrderStatusHistory>().Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = order.Status,
                    Comment = $"Se aprobó un pago de {Money(transaction.Amount)} con el pedido cancelado. Devuélvelo desde Pagos."
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private static string MethodName(PaymentTransaction transaction) => transaction.MethodId == "yape" ? "Yape" : "tarjeta";

        /// <summary>
        /// Historial del comprador: cada pedido crea su propio registro de cliente, así que se le reconoce
        /// por el celular (primer pedido = fecha de registro; último pedido pagado = última compra).
        /// </summary>
        private async Task<GatewayBuyerHistory> BuyerHistoryAsync(Order order, CancellationToken cancellationToken)
        {
            var phone = order.Customer!.Phone;
            var sameBuyer = db.Orders.Where(o => o.Id != order.Id && (o.CustomerId == order.CustomerId || o.Customer!.Phone == phone));

            var firstSeen = await sameBuyer.MinAsync(o => (DateTimeOffset?)o.CreatedAt, cancellationToken);
            var lastPurchase = await sameBuyer
                .Where(o => o.Payment != null && o.Payment.AmountPaid > 0)
                .MaxAsync(o => (DateTimeOffset?)o.CreatedAt, cancellationToken);

            var registeredAt = firstSeen is { } first && first < order.Customer.CreatedAt ? first : order.Customer.CreatedAt;
            return new GatewayBuyerHistory(registeredAt, lastPurchase, LocalPickup: order.DeliveryType == DeliveryType.MeetingPoint);
        }

        private static (string Id, string Type) ResolveMethod(CreateOnlinePaymentRequest request)
        {
            if (request.Method == "yape")
                return ("yape", "debit_card");

            var id = request.PaymentMethodId?.Trim().ToLowerInvariant();
            var type = request.PaymentTypeId?.Trim().ToLowerInvariant();
            if (id == null || !MethodIdPattern.IsMatch(id))
                throw new OnlinePaymentException("No pudimos identificar la tarjeta. Vuelve a ingresarla.");
            if (type is not ("credit_card" or "debit_card" or "prepaid_card"))
                throw new OnlinePaymentException("Tipo de tarjeta no válido.");
            return (id, type);
        }

        /// <summary>Mensaje para el cliente según el motivo del rechazo.</summary>
        public static string RejectionMessage(string? detail) => detail switch
        {
            null => "El pago fue rechazado. Prueba con otro medio de pago.",
            _ when detail.Contains("insufficient", StringComparison.OrdinalIgnoreCase) =>
                "El pago fue rechazado por fondos insuficientes. Prueba con otra tarjeta o con Yape.",
            _ when detail.Contains("call_for_authorize", StringComparison.OrdinalIgnoreCase) =>
                "Tu banco necesita que autorices el pago. Llama a tu banco o prueba con otro medio.",
            _ when detail.Contains("bad_filled", StringComparison.OrdinalIgnoreCase) || detail.Contains("security_code", StringComparison.OrdinalIgnoreCase) =>
                "Revisa los datos ingresados (número, vencimiento o código) e inténtalo de nuevo.",
            _ when detail.Contains("rejected_by_issuer", StringComparison.OrdinalIgnoreCase) || detail.Contains("rejected_by_bank", StringComparison.OrdinalIgnoreCase) =>
                "Tu banco rechazó el pago. Prueba con otra tarjeta o con Yape, o consulta con tu banco.",
            _ when detail.Contains("max_attempts", StringComparison.OrdinalIgnoreCase) =>
                "Superaste los intentos permitidos. Espera un momento o usa otro medio de pago.",
            _ when detail.Contains("invalid_card_token", StringComparison.OrdinalIgnoreCase) || detail.Contains("token", StringComparison.OrdinalIgnoreCase) =>
                "La sesión de pago expiró. Vuelve a ingresar tus datos.",
            _ when detail.Contains("duplicated", StringComparison.OrdinalIgnoreCase) =>
                "Este pago ya se había enviado. Revisa tu correo o el estado de tu pedido.",
            _ when detail.Contains("high_risk", StringComparison.OrdinalIgnoreCase) || detail.Contains("blacklist", StringComparison.OrdinalIgnoreCase) =>
                "Mercado Pago no pudo aprobar este pago. Prueba con otro medio de pago.",
            _ when detail.Contains("card_disabled", StringComparison.OrdinalIgnoreCase) =>
                "Tu tarjeta está desactivada para compras por internet. Actívala con tu banco o usa otra.",
            _ when detail.Contains("installments", StringComparison.OrdinalIgnoreCase) =>
                "Tu tarjeta no acepta esa cantidad de cuotas. Elige otra cantidad o paga en una cuota.",
            _ when detail.Contains("limit", StringComparison.OrdinalIgnoreCase) || detail.Contains("amount_rate", StringComparison.OrdinalIgnoreCase) =>
                "Superaste el límite de tu tarjeta o de tu Yape. Prueba con otro medio o un monto menor.",
            _ when detail.Contains("expired", StringComparison.OrdinalIgnoreCase) =>
                "Tu tarjeta está vencida. Usa otra tarjeta o paga con Yape.",
            _ when detail.Contains("otp", StringComparison.OrdinalIgnoreCase) =>
                "El código de Yape no es válido o ya venció. Genera uno nuevo en tu app Yape.",
            _ => "El pago fue rechazado. Prueba con otro medio de pago."
        };

        /// <summary>Consulta hasta 3 veces (a los 2, 3 y 4 s) si Mercado Pago creó la order de este intento.</summary>
        private async Task<GatewayOrder?> FindSoonAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
        {
            var step = verifyDelay ?? TimeSpan.FromSeconds(1);
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                await Task.Delay(step * (attempt + 1), cancellationToken);
                try
                {
                    if (await gateway.FindByExternalReferenceAsync(transaction.Id.ToString("N"), cancellationToken) is { Id: not null } order)
                        return order;
                }
                catch (MercadoPagoGatewayException ex)
                {
                    logger.LogWarning(ex, "No se pudo consultar el cobro {TransactionId} tras la respuesta en proceso", transaction.Id);
                }
            }
            return null;
        }

        /// <summary>
        /// Respuesta "en proceso": la página muestra "Confirmando tu pago…" y consulta el pedido hasta que se confirme.
        /// Nunca se le dice al cliente que falló cuando Mercado Pago pudo haber cobrado.
        /// </summary>
        private static OnlinePaymentResult InProgress(Order order, Payment payment, decimal amount) => new()
        {
            Status = "pending",
            Message = "Tu pago se está procesando. No pagues de nuevo: te avisamos apenas Mercado Pago lo confirme.",
            AmountCharged = amount,
            AmountPaid = payment.AmountPaid,
            BalanceDue = Math.Max(0m, payment.Amount - payment.AmountPaid),
            OrderStatus = order.Status.ToString()
        };

        private static string Money(decimal value) => $"S/ {value:F2}";

        private static string? Truncate(string? value, int max) => value == null ? null : value.Length <= max ? value : value[..max];
    }
}
