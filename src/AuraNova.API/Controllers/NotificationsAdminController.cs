using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.Notifications.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class NotificationsAdminController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsAdminController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet("orders/{orderId:guid}/notifications")]
        public async Task<IActionResult> GetByOrder(Guid orderId)
        {
            var notifications = await _notificationService.GetByOrderIdAsync(orderId);
            return Ok(notifications);
        }

        [HttpPost("orders/{orderId:guid}/notifications/{notificationId:guid}/prepare-whatsapp")]
        public async Task<IActionResult> PrepareWhatsApp(Guid orderId, Guid notificationId)
        {
            // Verify notification exists and belongs to the order
            var notification = await _notificationService.GetByIdAsync(notificationId);
            if (notification == null || notification.OrderId != orderId)
            {
                return NotFound(new { message = "Notificación no encontrada." });
            }

            var response = await _notificationService.PrepareWhatsAppAsync(notificationId);
            return Ok(response);
        }

        /// <summary>Vuelve a enviar un correo del pedido (por ejemplo, si falló o el cliente no lo recibió).</summary>
        [HttpPost("orders/{orderId:guid}/notifications/{notificationId:guid}/resend-email")]
        public async Task<IActionResult> ResendEmail(Guid orderId, Guid notificationId)
        {
            var notification = await _notificationService.GetByIdAsync(notificationId);
            if (notification == null || notification.OrderId != orderId)
                return NotFound(new { message = "Notificación no encontrada." });

            if (!await _notificationService.ResendEmailAsync(notificationId))
                return Problem(detail: "Solo se pueden reenviar correos, y el envío de correos debe estar configurado.", statusCode: StatusCodes.Status400BadRequest);

            return Accepted(new { message = "Correo en cola para reenvío." });
        }

        /// <summary>Bandeja: mensajes de WhatsApp listos que el equipo aún no abrió para enviar.</summary>
        [HttpGet("notifications/pending-whatsapp")]
        public async Task<IActionResult> GetPendingWhatsApp()
        {
            return Ok(await _notificationService.GetPendingWhatsAppAsync());
        }
    }
}
