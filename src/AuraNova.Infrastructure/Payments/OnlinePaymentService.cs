using System.Text.RegularExpressions;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Application.OnlinePayments.DTOs;
using AuraNova.Application.OnlinePayments.Interfaces;
using AuraNova.Application.Orders;
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
        ILogger<OnlinePaymentService> logger) : IOnlinePaymentService
    {
        // Un cobro "en proceso" bloquea otro intento durante este tiempo (evita pagar dos veces)
        private static readonly TimeSpan PendingLock = TimeSpan.FromMinutes(15);
        private static readonly Regex MethodIdPattern = new("^[a-z_]{2,30}$", RegexOptions.Compiled);

        public static decimal DepositFor(decimal total) => Math.Round(total / 2m, 2, MidpointRounding.AwayFromZero);

        public async Task<OnlinePaymentResult> PayAsync(Guid orderId, CreateOnlinePaymentRequest request, CancellationToken cancellationToken = default)
        {
            var order = await db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Payment!).ThenInclude(p => p.Transactions)
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

            if (payment.Transactions?.Any(t => t.ApprovedAt == null && t.Status is "processing" or "action_required" &&
                                               t.CreatedAt > DateTimeOffset.UtcNow - PendingLock) == true)
                throw new OnlinePaymentException("Tienes un pago en proceso. Espera unos minutos antes de intentar de nuevo.");

            // El servidor decide cuánto se cobra: adelanto, total o saldo
            var isDeposit = isFirstPayment && !request.PayFull;
            var amount = isFirstPayment ? (isDeposit ? DepositFor(payment.Amount) : payment.Amount) : balance;

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

            string? documentNumber = new string((request.IdentificationNumber ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
            if (documentNumber.Length == 0) documentNumber = null;

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
                    Description: $"Pedido {order.OrderCode} - Aura Nova",
                    PayerEmail: email,
                    // La API exige el número si se envía el documento: solo va cuando el cliente lo ingresó
                    IdentificationType: documentNumber == null ? null : request.IdentificationType?.Trim() is { Length: > 0 } type ? type : "DNI",
                    IdentificationNumber: documentNumber,
                    PhoneNumber: phone,
                    PaymentMethodId: methodId,
                    PaymentMethodType: methodType,
                    Token: request.Token.Trim(),
                    Installments: request.Installments,
                    IdempotencyKey: MercadoPagoGateway.IdempotencyKeyFor(request.Token.Trim())), cancellationToken);
            }
            catch (MercadoPagoGatewayException ex)
            {
                transaction.Status = "error";
                transaction.StatusDetail = Truncate(ex.Message, 100);
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

            var paid = payment.AmountPaid;
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

        /// <summary>Guarda el resultado de Mercado Pago y, si se aprobó, suma el pago al pedido una sola vez.</summary>
        private async Task ApplyAsync(Order order, Payment payment, PaymentTransaction transaction, GatewayOrder result, CancellationToken cancellationToken)
        {
            transaction.ExternalOrderId ??= result.Id;
            transaction.ExternalPaymentId ??= result.Payments.FirstOrDefault()?.Id;
            transaction.Status = Truncate(result.Status ?? "unknown", 40)!;
            transaction.StatusDetail = Truncate(result.Detail, 100);
            transaction.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

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
                    return;

                transaction.ApprovedAt = approvedAt;
                db.Entry(transaction).Property(t => t.ApprovedAt).IsModified = false;
            }
            else
            {
                transaction.ApprovedAt = approvedAt;
                await db.SaveChangesAsync(cancellationToken);
            }

            var wasFirstPayment = payment.AmountPaid <= 0;
            var approvedTotal = await db.PaymentTransactions
                .Where(t => t.PaymentId == payment.Id && t.ApprovedAt != null)
                .SumAsync(t => t.Amount, cancellationToken);

            payment.AmountPaid = Math.Min(approvedTotal, payment.Amount);
            payment.Method = PaymentMethod.MercadoPago;
            payment.Status = PaymentStatus.Confirmed;
            payment.VerifiedAt ??= DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;

            var due = payment.Amount - payment.AmountPaid;
            var method = transaction.MethodId == "yape" ? "Yape" : "tarjeta";
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
            _ => "El pago fue rechazado. Prueba con otro medio de pago."
        };

        private static string Money(decimal value) => $"S/ {value:F2}";

        private static string? Truncate(string? value, int max) => value == null ? null : value.Length <= max ? value : value[..max];
    }
}
