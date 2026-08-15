using System.Threading.Tasks;
using AuraNova.Application.Orders.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/public/orders")]
    public class TrackingController : ControllerBase
    {
        private readonly IOrderTrackingService _trackingService;

        public TrackingController(IOrderTrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        [HttpGet("{orderCode}/tracking/{trackingToken}")]
        [EnableRateLimiting("tracking_policy")]
        public async Task<IActionResult> GetTracking(string orderCode, string trackingToken)
        {
            var result = await _trackingService.GetTrackingAsync(orderCode, trackingToken);
            if (result == null)
                return NotFound(new { message = "Pedido no encontrado o token de seguimiento inválido." });

            return Ok(result);
        }
    }
}
