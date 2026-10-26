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
            var secret = settings.Value.WebhookSecret;

            if (!string.IsNullOrWhiteSpace(secret))
            {
                try
                {
                    WebhookSignatureValidator.Validate(signature, requestId, dataId, secret);
                }
                catch (InvalidWebhookSignatureException ex)
                {
                    logger.LogWarning("Webhook de Mercado Pago con firma inválida ({Reason}), request {RequestId}", ex.Reason, requestId);
                    return Unauthorized();
                }
            }
            else
            {
                logger.LogWarning("MercadoPago__WebhookSecret no está configurado: el webhook se procesa sin validar la firma");
            }

            // Solo nos interesan las orders; el resto se confirma para que Mercado Pago no reintente
            if (!string.Equals(type, "order", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(dataId))
                return Ok();

            try
            {
                await onlinePayments.HandleOrderNotificationAsync(dataId, cancellationToken);
            }
            catch (MercadoPagoGatewayException ex)
            {
                // 500: Mercado Pago reintentará el aviso más tarde
                logger.LogError(ex, "No se pudo procesar el webhook de la order {OrderId}", dataId);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }
    }
}
