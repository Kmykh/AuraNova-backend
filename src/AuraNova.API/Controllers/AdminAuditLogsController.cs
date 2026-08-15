using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.Audit.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/audit-logs")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class AdminAuditLogsController : ControllerBase
    {
        private readonly IAdminAuditService _auditService;

        public AdminAuditLogsController(IAdminAuditService auditService)
        {
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? action,
            [FromQuery] string? entityType,
            [FromQuery] Guid? adminUserId,
            [FromQuery] DateTimeOffset? dateFrom,
            [FromQuery] DateTimeOffset? dateTo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _auditService.GetAuditLogsAsync(action, entityType, adminUserId, dateFrom, dateTo, page, pageSize);
            return Ok(result);
        }
    }
}
