using System;
using System.Threading.Tasks;
using AuraNova.Application.LiveBroadcast.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AuraNova.API.Hubs
{
    public class SuperAdminLiveHub : Hub
    {
        private readonly ILiveBroadcastStateService _stateService;

        public SuperAdminLiveHub(ILiveBroadcastStateService stateService)
        {
            _stateService = stateService;
        }

        public override async Task OnConnectedAsync()
        {
            // Send current state to the connecting client immediately
            var currentState = _stateService.GetState();
            await Clients.Caller.SendAsync("ReceiveLiveState", currentState);
            await base.OnConnectedAsync();
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task StreamLiveText(string? text)
        {
            _stateService.UpdateLiveText(text);
            await Clients.All.SendAsync("ReceiveLiveTyping", text);
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task ToggleTikTokLive(bool isActive, string? username)
        {
            _stateService.SetTikTokLiveState(isActive, username);
            await Clients.All.SendAsync("ReceiveTikTokLiveState", new
            {
                isActive,
                username = isActive ? username : null
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task BroadcastTikTokComment(TikTokCommentDto comment)
        {
            if (comment == null || string.IsNullOrWhiteSpace(comment.Comment))
                return;

            comment.Timestamp = DateTimeOffset.UtcNow;
            await Clients.All.SendAsync("ReceiveTikTokComment", comment);
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task UpdateTikTokStats(int viewerCount, long totalLikes)
        {
            _stateService.UpdateTikTokStats(viewerCount, totalLikes);
            await Clients.All.SendAsync("ReceiveTikTokStats", new
            {
                viewerCount,
                totalLikes
            });
        }
    }
}
