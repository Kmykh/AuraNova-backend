using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.Dashboard.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class DashboardAdminController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardAdminController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var summary = await _dashboardService.GetSummaryAsync();
            return Ok(summary);
        }
    }
}
