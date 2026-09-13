using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraNova.API.Hubs;
using AuraNova.Application.LiveBroadcast.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TikTokLiveSharp.Client;
using TikTokLiveSharp.Events;

namespace AuraNova.API.Services
{
    public interface ITikTokIntegrationManager
    {
        Task ConnectAsync(string username);
        Task DisconnectAsync();
    }

    public class TikTokIntegrationManager : ITikTokIntegrationManager
    {
        private readonly ILogger<TikTokIntegrationManager> _logger;
        private readonly IHubContext<Hubs.SuperAdminLiveHub> _hubContext;
        private readonly Application.LiveBroadcast.Interfaces.ILiveBroadcastStateService _stateService;
        private TikTokLiveClient? _client;

        public TikTokIntegrationManager(
            ILogger<TikTokIntegrationManager> logger,
            IHubContext<Hubs.SuperAdminLiveHub> hubContext,
            Application.LiveBroadcast.Interfaces.ILiveBroadcastStateService stateService)
        {
            _logger = logger;
            _hubContext = hubContext;
            _stateService = stateService;
        }

        public Task ConnectAsync(string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username)) return Task.CompletedTask;

                // Stop existing connection if any
                if (_client != null)
                {
                    _client.Stop();
                    _client = null;
                }

                _logger.LogInformation("Initializing TikTok Live connection for user: {Username}", username);

                // Initialize client
                _client = new TikTokLiveClient(username, "");

                // Register event handlers
                _client.OnChatMessage += Client_OnChatMessage;
                _client.OnLike += Client_OnLike;
                _client.OnRoomUpdate += Client_OnRoomUpdate;

                _ = Task.Run(() => 
                {
                    try
                    {
                        _client.Run(new CancellationToken());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background task error in TikTok Live Client.");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to TikTok Live for username: {Username}", username);
            }
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            if (_client != null)
            {
                try
                {
                    _logger.LogInformation("Disconnecting from TikTok Live.");
                    _client.OnChatMessage -= Client_OnChatMessage;
                    _client.OnLike -= Client_OnLike;
                    _client.OnRoomUpdate -= Client_OnRoomUpdate;
                    _client.Stop();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disconnecting from TikTok Live.");
                }
                finally
                {
                    _client = null;
                }
            }
            return Task.CompletedTask;
        }

        private async void Client_OnRoomUpdate(TikTokLiveClient sender, TikTokLiveSharp.Events.RoomUpdate e)
        {
            try
            {
                var state = _stateService.GetState();
                var viewerCount = (int)e.NumberOfViewers;
                _stateService.UpdateTikTokStats(viewerCount, state.TotalLikes);
                await _hubContext.Clients.All.SendAsync("ReceiveTikTokStats", new
                {
                    viewerCount = viewerCount,
                    totalLikes = state.TotalLikes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling RoomUpdate.");
            }
        }

        private async void Client_OnLike(TikTokLiveClient sender, TikTokLiveSharp.Events.Like e)
        {
            try
            {
                var state = _stateService.GetState();
                var newTotal = state.TotalLikes + (int)e.Count;
                _stateService.UpdateTikTokStats(state.ViewerCount, newTotal);
                await _hubContext.Clients.All.SendAsync("ReceiveTikTokStats", new
                {
                    viewerCount = state.ViewerCount,
                    totalLikes = newTotal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Like.");
            }
        }

        private async void Client_OnChatMessage(TikTokLiveClient sender, TikTokLiveSharp.Events.Chat e)
        {
            try
            {
                var dto = new TikTokCommentDto
                {
                    Username = e.Sender.UniqueId,
                    Comment = e.Message,
                    UserAvatarUrl = e.Sender.AvatarThumbnail?.Urls?.FirstOrDefault() ?? "",
                    Timestamp = DateTimeOffset.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("ReceiveTikTokComment", dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ChatMessage.");
            }
        }
    }
}
