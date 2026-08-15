using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.DeliveryZones.DTOs;
using AuraNova.Application.DeliveryZones.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/delivery-zones")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class DeliveryZonesAdminController : ControllerBase
    {
        private readonly IDeliveryZoneService _service;

        public DeliveryZonesAdminController(IDeliveryZoneService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeliveryZoneRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var zone = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = zone.Id }, zone);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var zones = await _service.GetAllAsync();
            return Ok(zones);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var zone = await _service.GetByIdAsync(id);
            if (zone == null) return NotFound();
            return Ok(zone);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeliveryZoneRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var zone = await _service.UpdateAsync(id, request);
            if (zone == null) return NotFound();
            return Ok(zone);
        }

        [HttpPatch("{id:guid}/availability")]
        public async Task<IActionResult> UpdateAvailability(Guid id, [FromBody] UpdateAvailabilityRequest request)
        {
            var result = await _service.UpdateAvailabilityAsync(id, request.IsActive);
            if (!result) return NotFound();
            return Ok(new { message = "Disponibilidad actualizada correctamente." });
        }
    }
}
