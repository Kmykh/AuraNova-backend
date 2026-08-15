using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.DeliveryZones.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/delivery-zones")]
    public class DeliveryZonesController : ControllerBase
    {
        private readonly IDeliveryZoneService _service;

        public DeliveryZonesController(IDeliveryZoneService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns only active delivery zones. Public endpoint — no authentication required.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPublic()
        {
            var zones = await _service.GetPublicAsync();
            return Ok(zones);
        }
    }
}
