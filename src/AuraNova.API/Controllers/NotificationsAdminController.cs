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
    }
}
