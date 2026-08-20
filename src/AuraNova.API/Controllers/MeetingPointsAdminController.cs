using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.MeetingPoints.DTOs;
using AuraNova.Application.DeliveryZones.DTOs;
using AuraNova.Application.MeetingPoints.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.API.Extensions;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/meeting-points")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class MeetingPointsAdminController : ControllerBase
    {
        private readonly IMeetingPointService _service;
        private readonly IAdminAuditService _auditService;

        public MeetingPointsAdminController(IMeetingPointService service, IAdminAuditService auditService)
        {
            _service = service;
            _auditService = auditService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMeetingPointRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var point = await _service.CreateAsync(request);
            
            await this.LogActionAsync(_auditService, "CreateMeetingPoint", "MeetingPoint", point.Id.ToString(), $"Punto de encuentro '{point.Name}' creado.");
            
            return CreatedAtAction(nameof(GetById), new { id = point.Id }, point);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var points = await _service.GetAllAsync();
            return Ok(points);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var point = await _service.GetByIdAsync(id);
            if (point == null) return NotFound();
            return Ok(point);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingPointRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var point = await _service.UpdateAsync(id, request);
            if (point == null) return NotFound();
            
            await this.LogActionAsync(_auditService, "UpdateMeetingPoint", "MeetingPoint", point.Id.ToString(), $"Punto de encuentro '{point.Name}' editado.");
            
            return Ok(point);
        }

        [HttpPatch("{id:guid}/availability")]
        public async Task<IActionResult> UpdateAvailability(Guid id, [FromBody] UpdateAvailabilityRequest request)
        {
            var result = await _service.UpdateAvailabilityAsync(id, request.IsActive);
            if (!result) return NotFound();
            
            await this.LogActionAsync(_auditService, "UpdateMeetingPointAvailability", "MeetingPoint", id.ToString(), $"Disponibilidad de punto de encuentro cambiada a {(request.IsActive ? "Activo" : "Inactivo")}.");
            
            return Ok(new { message = "Disponibilidad actualizada correctamente." });
        }
    }
}
