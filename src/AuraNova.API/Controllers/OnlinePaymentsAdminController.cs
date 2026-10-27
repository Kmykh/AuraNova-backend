using AuraNova.Application.OnlinePayments.DTOs;
using AuraNova.Application.OnlinePayments.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    /// <summary>Panel: devoluciones, reporte de cobros en línea y estado de los avisos de Mercado Pago.</summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class OnlinePaymentsAdminController(IOnlinePaymentService onlinePayments) : ControllerBase
    {
        /// <summary>Devuelve un cobro aprobado (total, o parcial si se indica el monto).</summary>
        [HttpPost("orders/{orderId:guid}/payments/{transactionId:guid}/refund")]
        public async Task<IActionResult> Refund(Guid orderId, Guid transactionId, [FromBody] RefundOnlinePaymentRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                return Ok(await onlinePayments.RefundAsync(orderId, transactionId, request, cancellationToken));
            }
            catch (OnlinePaymentException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }

        /// <summary>
        /// "Verificar con Mercado Pago": revisa ya los cobros sin aprobar del pedido (por si Mercado Pago cobró
        /// pero la respuesta no llegó) y devuelve cuántos cambiaron.
        /// </summary>
        [HttpPost("orders/{orderId:guid}/payments/reconcile")]
        public async Task<IActionResult> Reconcile(Guid orderId, CancellationToken cancellationToken) =>
            Ok(new { changed = await onlinePayments.ReconcileOrderAsync(orderId, cancellationToken) });

        /// <summary>Cobros en línea del periodo (por defecto, los últimos 30 días) con totales.</summary>
        [HttpGet("payments/online")]
        public async Task<IActionResult> Report([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
        {
            var end = to ?? DateTimeOffset.UtcNow;
            var start = from ?? end.AddDays(-30);
            if (start >= end || end - start > TimeSpan.FromDays(366))
                return Problem(detail: "Elige un rango de fechas válido (máximo un año).", statusCode: StatusCodes.Status400BadRequest);

            return Ok(await onlinePayments.GetReportAsync(start, end, cancellationToken));
        }

        /// <summary>Últimos avisos (webhooks) recibidos de Mercado Pago y su resultado.</summary>
        [HttpGet("payments/webhooks")]
        public async Task<IActionResult> Webhooks([FromQuery] int take = 20, CancellationToken cancellationToken = default) =>
            Ok(await onlinePayments.GetWebhookEventsAsync(take, cancellationToken));
    }
}
