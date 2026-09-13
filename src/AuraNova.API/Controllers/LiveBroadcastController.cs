using System.Threading.Tasks;
using AuraNova.API.Hubs;
using AuraNova.Application.LiveBroadcast.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/live")]
    public class LiveBroadcastController : ControllerBase
    {
        private readonly ILiveBroadcastStateService _stateService;
        private readonly IHubContext<SuperAdminLiveHub> _hubContext;

        public LiveBroadcastController(
            ILiveBroadcastStateService stateService,
            IHubContext<SuperAdminLiveHub> hubContext)
        {
            _stateService = stateService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Public endpoint to fetch current active live broadcast state on page load.
        /// </summary>
        [HttpGet("state")]
        public IActionResult GetState()
        {
            return Ok(_stateService.GetState());
        }

        /// <summary>
        /// REST endpoint for SuperAdmin to update live text.
        /// </summary>
        [HttpPost("stream-text")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> StreamText([FromBody] StreamTextRequest request)
        {
            _stateService.UpdateLiveText(request.Text);
            await _hubContext.Clients.All.SendAsync("ReceiveLiveTyping", request.Text);
            return Ok(new { message = "Texto transmitido exitosamente.", text = request.Text });
        }
    }

    public class StreamTextRequest
    {
        public string? Text { get; set; }
    }
}
