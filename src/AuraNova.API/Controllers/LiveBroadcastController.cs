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
        private readonly AuraNova.API.Services.ITikTokIntegrationManager _tikTokManager;

        public LiveBroadcastController(
            ILiveBroadcastStateService stateService,
            IHubContext<SuperAdminLiveHub> hubContext,
            AuraNova.API.Services.ITikTokIntegrationManager tikTokManager)
        {
            _stateService = stateService;
            _hubContext = hubContext;
            _tikTokManager = tikTokManager;
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

        /// <summary>
        /// REST endpoint for SuperAdmin to toggle TikTok Live state.
        /// </summary>
        [HttpPost("tiktok-toggle")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ToggleTikTok([FromBody] ToggleTikTokRequest request)
        {
            if (request.IsActive && !string.IsNullOrWhiteSpace(request.Username))
            {
                await _tikTokManager.ConnectAsync(request.Username);
            }
            else
            {
                await _tikTokManager.DisconnectAsync();
            }

            _stateService.SetTikTokLiveState(request.IsActive, request.Username);
            await _hubContext.Clients.All.SendAsync("ReceiveTikTokLiveState", new
            {
                isActive = request.IsActive,
                tikTokUsername = request.IsActive ? request.Username : null
            });
            return Ok(new { message = "Estado de TikTok Live actualizado.", isActive = request.IsActive, username = request.Username });
        }
    }

    public class StreamTextRequest
    {
        public string? Text { get; set; }
    }

    public class ToggleTikTokRequest
    {
        public bool IsActive { get; set; }
        public string? Username { get; set; }
    }
}
