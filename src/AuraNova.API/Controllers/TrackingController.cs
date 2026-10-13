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

        /// <summary>
        /// Seguimiento con un solo código ("K7M3-Q9XA"). Es lo que usan el enlace /seguimiento/{código} y el buscador.
        /// </summary>
        [HttpGet("/api/public/tracking/{trackingCode}")]
        [EnableRateLimiting("tracking_policy")]
        public async Task<IActionResult> GetTrackingByCode(string trackingCode)
        {
            var result = await _trackingService.GetTrackingByCodeAsync(trackingCode);
            if (result == null)
                return NotFound(new { message = "No encontramos un pedido con ese código de seguimiento." });

            return Ok(result);
        }

        /// <summary>Formato anterior (número de pedido + token). Se mantiene para enlaces ya enviados.</summary>
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
