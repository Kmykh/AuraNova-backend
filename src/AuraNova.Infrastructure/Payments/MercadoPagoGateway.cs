using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MercadoPago.Client;
using MercadoPago.Client.Order;
using MercadoPago.Error;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace AuraNova.Infrastructure.Payments
{
    public class MercadoPagoSettings
    {
        /// <summary>Access Token (de prueba o de producción). Solo backend: variable MercadoPago__AccessToken.</summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Firma secreta de "Configuración de Webhooks" (MercadoPago__WebhookSecret).</summary>
        public string WebhookSecret { get; set; } = string.Empty;
    }

    /// <summary>Lo que enviamos para crear una order de Mercado Pago con un solo pago.</summary>
    public record GatewayCreateOrder(
        string ExternalReference,
        decimal Amount,
        string Description,
        string PayerEmail,
        string? IdentificationType,
        string? IdentificationNumber,
        string? PhoneNumber,
        string PaymentMethodId,
        string PaymentMethodType,
        string Token,
        int? Installments,
        string IdempotencyKey);

    public record GatewayPayment(string? Id, string? Status, string? StatusDetail, string? MethodId, string? MethodType);

    /// <summary>Estado de una order de Mercado Pago (solo lo que necesitamos).</summary>
    public record GatewayOrder(
        string? Id,
        string? Status,
        string? StatusDetail,
        string? ExternalReference,
        decimal TotalPaidAmount,
        IReadOnlyList<GatewayPayment> Payments)
    {
        /// <summary>Pago aprobado y acreditado.</summary>
        public bool IsApproved => Status == "processed" &&
                                  (Payments.Count == 0 || Payments.All(p => p.Status == null || p.Status == "processed"));

        public bool IsFailed => Status is "failed" or "canceled" or "cancelled" or "expired" ||
                                Payments.Any(p => p.Status == "failed");

        /// <summary>Motivo más específico: el del pago (p. ej. insufficient_amount) antes que el de la order.</summary>
        public string? Detail => Payments.FirstOrDefault()?.StatusDetail ?? StatusDetail;
    }

    /// <summary>Error al comunicarse con Mercado Pago (credenciales, datos inválidos, servicio caído).</summary>
    public class MercadoPagoGatewayException(string message, Exception? inner = null) : Exception(message, inner);

    public interface IMercadoPagoGateway
    {
        Task<GatewayOrder> CreateOrderAsync(GatewayCreateOrder request, CancellationToken cancellationToken = default);
        Task<GatewayOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);
    }

    /// <summary>Checkout API con la API de Orders, usando el SDK oficial (mercadopago-sdk).</summary>
    public class MercadoPagoGateway(IOptions<MercadoPagoSettings> options, ILogger<MercadoPagoGateway> logger) : IMercadoPagoGateway
    {
        private readonly MercadoPagoSettings _settings = options.Value;
        private readonly OrderClient _client = new();

        public async Task<GatewayOrder> CreateOrderAsync(GatewayCreateOrder request, CancellationToken cancellationToken = default)
        {
            var amount = request.Amount.ToString("0.00", CultureInfo.InvariantCulture);
            var isYape = request.PaymentMethodId == "yape";

            var body = new OrderCreateRequest
            {
                Type = "online",
                ProcessingMode = "automatic",
                ExternalReference = request.ExternalReference,
                TotalAmount = amount,
                Description = request.Description,
                Payer = new OrderPayerRequest
                {
                    Email = request.PayerEmail,
                    EntityType = isYape ? "individual" : null,
                    Identification = string.IsNullOrWhiteSpace(request.IdentificationType)
                        ? null
                        : new OrderIdentificationRequest
                        {
                            Type = request.IdentificationType,
                            Number = string.IsNullOrWhiteSpace(request.IdentificationNumber) ? null : request.IdentificationNumber
                        },
                    Phone = string.IsNullOrWhiteSpace(request.PhoneNumber)
                        ? null
                        : new OrderPhoneRequest { AreaCode = "51", Number = request.PhoneNumber }
                },
                Transactions = new OrderTransactionRequest
                {
                    Payments = new List<OrderPaymentRequest>
                    {
                        new()
                        {
                            Amount = amount,
                            PaymentMethod = new OrderPaymentMethodRequest
                            {
                                Id = request.PaymentMethodId,
                                Type = request.PaymentMethodType,
                                Token = request.Token,
                                Installments = isYape ? null : request.Installments ?? 1
                            }
                        }
                    }
                }
            };

            try
            {
                var order = await _client.CreateAsync(body, Options(request.IdempotencyKey), cancellationToken);
                return Map(order);
            }
            catch (MercadoPagoApiException ex)
            {
                // Un pago rechazado puede volver como error HTTP (p. ej. 402) con la order en el cuerpo
                var parsed = TryParse(ex.ApiResponse?.Content);
                if (parsed?.Id != null)
                    return parsed;

                logger.LogWarning("Mercado Pago rechazó la creación de la order ({Status}): {Error}",
                    ex.StatusCode, ex.ApiError?.Message ?? ex.Message);
                throw new MercadoPagoGatewayException(FriendlyApiError(ex), ex);
            }
            catch (MercadoPagoException ex)
            {
                logger.LogError(ex, "No se pudo conectar con Mercado Pago");
                throw new MercadoPagoGatewayException("No pudimos conectar con Mercado Pago. Inténtalo en unos minutos.", ex);
            }
        }

        public async Task<GatewayOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                return Map(await _client.GetAsync(orderId, Options(null), cancellationToken));
            }
            catch (MPNotFoundException)
            {
                return null;
            }
            catch (Exception ex) when (ex is MercadoPagoApiException or MercadoPagoException)
            {
                throw new MercadoPagoGatewayException("No pudimos consultar el pago en Mercado Pago.", ex);
            }
        }

        private RequestOptions Options(string? idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(_settings.AccessToken))
                throw new MercadoPagoGatewayException("Los pagos en línea no están configurados (falta MercadoPago__AccessToken).");

            var options = new RequestOptions { AccessToken = _settings.AccessToken };
            if (idempotencyKey != null)
                options.CustomHeaders.Add("X-Idempotency-Key", idempotencyKey);
            return options;
        }

        /// <summary>Misma clave para el mismo token: si el navegador reenvía el pago, Mercado Pago no cobra dos veces.</summary>
        public static string IdempotencyKeyFor(string token)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return new Guid(hash.AsSpan(0, 16)).ToString();
        }

        private static GatewayOrder Map(MercadoPago.Resource.Order.Order order) => new(
            order.Id,
            order.Status,
            order.StatusDetail,
            order.ExternalReference,
            ParseAmount(order.TotalPaidAmount),
            (order.Transactions?.Payments ?? [])
                .Select(p => new GatewayPayment(p.Id, p.Status, p.StatusDetail, p.PaymentMethod?.Id, p.PaymentMethod?.Type))
                .ToList());

        public static GatewayOrder? TryParse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                var root = JObject.Parse(json);
                // A veces la order viene directo y otras dentro de "data"
                var order = root["id"] != null ? root : root["data"] as JObject;
                if (order?["id"] == null)
                    return null;

                var payments = (order.SelectToken("transactions.payments") as JArray ?? [])
                    .OfType<JObject>()
                    .Select(p => new GatewayPayment(
                        (string?)p["id"], (string?)p["status"], (string?)p["status_detail"],
                        (string?)p.SelectToken("payment_method.id"), (string?)p.SelectToken("payment_method.type")))
                    .ToList();

                return new GatewayOrder(
                    (string?)order["id"], (string?)order["status"], (string?)order["status_detail"],
                    (string?)order["external_reference"], ParseAmount((string?)order["total_paid_amount"]), payments);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        private static decimal ParseAmount(string? value) =>
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

        private static string FriendlyApiError(MercadoPagoApiException ex) => ex.StatusCode switch
        {
            401 or 403 => "Los pagos en línea no están disponibles en este momento.",
            429 => "Hay muchos intentos de pago seguidos. Espera un minuto e inténtalo de nuevo.",
            >= 500 => "Mercado Pago no responde en este momento. Inténtalo en unos minutos.",
            _ => "No pudimos procesar el pago. Revisa los datos e inténtalo de nuevo."
        };
    }
}
