using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.DeliveryZones.DTOs;
using AuraNova.Application.DeliveryZones.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.API.Extensions;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/delivery-zones")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class DeliveryZonesAdminController : ControllerBase
    {
        private readonly IDeliveryZoneService _service;
        private readonly IAdminAuditService _auditService;

        public DeliveryZonesAdminController(IDeliveryZoneService service, IAdminAuditService auditService)
        {
            _service = service;
            _auditService = auditService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeliveryZoneRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var zone = await _service.CreateAsync(request);
            
            await this.LogActionAsync(_auditService, "CreateDeliveryZone", "DeliveryZone", zone.Id.ToString(), $"Zona de reparto '{zone.Name}' creada.");
            
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
            
            await this.LogActionAsync(_auditService, "UpdateDeliveryZone", "DeliveryZone", zone.Id.ToString(), $"Zona de reparto '{zone.Name}' editada.");
            
            return Ok(zone);
        }

        [HttpPatch("{id:guid}/availability")]
        public async Task<IActionResult> UpdateAvailability(Guid id, [FromBody] UpdateAvailabilityRequest request)
        {
            var result = await _service.UpdateAvailabilityAsync(id, request.IsActive);
            if (!result) return NotFound();
            
            await this.LogActionAsync(_auditService, "UpdateDeliveryZoneAvailability", "DeliveryZone", id.ToString(), $"Disponibilidad de zona cambiada a {(request.IsActive ? "Activo" : "Inactivo")}.");
            
            return Ok(new { message = "Disponibilidad actualizada correctamente." });
        }
    }
}
