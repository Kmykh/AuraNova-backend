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
        private readonly AuraNova.Application.BusinessSettings.Interfaces.IBusinessSettingsService _businessSettingsService;

        public SuperAdminLiveHub(
            ILiveBroadcastStateService stateService,
            AuraNova.Application.BusinessSettings.Interfaces.IBusinessSettingsService businessSettingsService)
        {
            _stateService = stateService;
            _businessSettingsService = businessSettingsService;
        }

        public override async Task OnConnectedAsync()
        {
            // Send current state to the connecting client immediately
            var currentState = _stateService.GetState();
            await Clients.Caller.SendAsync("ReceiveLiveState", currentState);
            await base.OnConnectedAsync();
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task ToggleLiveText(bool isActive)
        {
            _stateService.ToggleLiveText(isActive);
            await Clients.All.SendAsync("ReceiveLiveTextState", isActive);
            
            // If turned off, also clear the current text on all clients
            if (!isActive)
            {
                await Clients.All.SendAsync("ReceiveLiveTyping", null);
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task StreamLiveText(string? text)
        {
            var currentState = _stateService.GetState();
            if (!currentState.IsLiveTextActive)
                return; // Backend strictly blocks message broadcasting if mode is OFF

            _stateService.UpdateLiveText(text);
            await Clients.All.SendAsync("ReceiveLiveTyping", text);
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task ToggleTikTokLive(bool isActive, string? username = null)
        {
            string? usernameToUse = username;
            if (isActive && string.IsNullOrWhiteSpace(usernameToUse))
            {
                var settings = await _businessSettingsService.GetAdminAsync();
                usernameToUse = settings.TikTokUsername;
            }

            _stateService.SetTikTokLiveState(isActive, usernameToUse);
            await Clients.All.SendAsync("ReceiveTikTokLiveState", new
            {
                isActive,
                tikTokUsername = isActive ? usernameToUse : null
            });
        }
    }
}
