using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.MeetingPoints.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/meeting-points")]
    public class MeetingPointsController : ControllerBase
    {
        private readonly IMeetingPointService _service;

        public MeetingPointsController(IMeetingPointService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns only active meeting points. Public endpoint — no authentication required.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPublic()
        {
            var points = await _service.GetPublicAsync();
            return Ok(points);
        }
    }
}
