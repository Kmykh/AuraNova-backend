using AuraNova.Application.OnlinePayments.DTOs;
using AuraNova.Application.OnlinePayments.Interfaces;
using AuraNova.Infrastructure.Payments;
using MercadoPago.Error;
using MercadoPago.Webhook;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuraNova.API.Controllers
{
    /// <summary>Pagos en la página con Mercado Pago (tarjeta y Yape) y sus notificaciones.</summary>
    [ApiController]
    public class OnlinePaymentsController(
        IOnlinePaymentService onlinePayments,
        IOptions<MercadoPagoSettings> settings,
        ILogger<OnlinePaymentsController> logger) : ControllerBase
    {
        /// <summary>
        /// Cobra el adelanto del 50%, el total o el saldo del pedido. El monto lo calcula el servidor;
        /// el navegador solo envía el token de un solo uso generado por MercadoPago.js.
        /// </summary>
        /// <summary>Public Key de Mercado Pago para MercadoPago.js (no es secreta). Null si no está configurada.</summary>
        [HttpGet("api/payments/mercadopago/config")]
        public IActionResult Config()
        {
            var publicKey = settings.Value.PublicKey?.Trim();
            Response.Headers.CacheControl = "public, max-age=300";
            return Ok(new { publicKey = string.IsNullOrEmpty(publicKey) ? null : publicKey });
        }

        [HttpPost("api/orders/{orderId:guid}/payments/mercadopago")]
        [EnableRateLimiting("online_payment_policy")]
        public async Task<IActionResult> Pay(Guid orderId, [FromBody] CreateOnlinePaymentRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                var result = await onlinePayments.PayAsync(orderId, request, cancellationToken);
                Response.Headers.CacheControl = "no-store";
                return Ok(result);
            }
            catch (OnlinePaymentException ex)
            {
                // ProblemDetails: el frontend muestra "detail" tal cual al cliente
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "No se pudo procesar el pago");
            }
        }

        /// <summary>
        /// Webhook del tópico Order de Mercado Pago. Se valida la firma (x-signature) y luego se consulta la order
        /// en Mercado Pago: el contenido del aviso nunca se usa para marcar un pago.
        /// </summary>
        [HttpPost("api/webhooks/mercadopago")]
        public async Task<IActionResult> Webhook(
            [FromQuery(Name = "data.id")] string? dataId,
            [FromQuery] string? type,
            CancellationToken cancellationToken)
        {
            var signature = Request.Headers["x-signature"].ToString();
            var requestId = Request.Headers["x-request-id"].ToString();
            // Se limpian espacios o saltos de línea que se cuelan al pegar la clave en Render
            var secret = settings.Value.WebhookSecret?.Trim();

            if (!string.IsNullOrWhiteSpace(secret))
            {
                try
                {
                    ValidateSignature(signature, requestId, dataId, secret);
                }
                catch (InvalidWebhookSignatureException ex)
                {
                    logger.LogWarning("Webhook de Mercado Pago con firma inválida ({Reason}), request {RequestId}", ex.Reason, requestId);
                    await LogWebhookAsync(type, dataId, "invalid_signature", $"Firma inválida ({ex.Reason}). Revisa MercadoPago__WebhookSecret.");
                    return Unauthorized();
                }
            }
            else
            {
                logger.LogWarning("MercadoPago__WebhookSecret no está configurado: el webhook se procesa sin validar la firma");
            }

            // Solo nos interesan las orders; el resto se confirma para que Mercado Pago no reintente
            if (!string.Equals(type, "order", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(dataId))
            {
                await LogWebhookAsync(type, dataId, "ignored", "Tópico que no usamos.");
                return Ok();
            }

            try
            {
                await onlinePayments.HandleOrderNotificationAsync(dataId, cancellationToken);
            }
            catch (MercadoPagoGatewayException ex)
            {
                // 500: Mercado Pago reintentará el aviso más tarde
                logger.LogError(ex, "No se pudo procesar el webhook de la order {OrderId}", dataId);
                await LogWebhookAsync(type, dataId, "error", ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            await LogWebhookAsync(type, dataId, "processed", null);
            return Ok();
        }

        /// <summary>
        /// Mercado Pago firma el data.id en minúsculas (los ids de Orders llevan letras: ORD01...), pero el SDK de .NET
        /// lo usa tal cual llega y la firma nunca coincidía. Se acepta también tal cual, por compatibilidad.
        /// </summary>
        private static void ValidateSignature(string signature, string requestId, string? dataId, string secret)
        {
            var lowercaseId = dataId?.ToLowerInvariant();
            try
            {
                WebhookSignatureValidator.Validate(signature, requestId, lowercaseId, secret);
            }
            catch (InvalidWebhookSignatureException) when (dataId != lowercaseId)
            {
                WebhookSignatureValidator.Validate(signature, requestId, dataId, secret);
            }
        }

        /// <summary>El registro de avisos es solo informativo: si falla, no afecta la respuesta a Mercado Pago.</summary>
        private async Task LogWebhookAsync(string? topic, string? resourceId, string result, string? detail)
        {
            try
            {
                await onlinePayments.LogWebhookAsync(topic, resourceId, result, detail);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo registrar el aviso de Mercado Pago");
            }
        }
    }
}
