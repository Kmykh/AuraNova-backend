using System;
using System.Linq;
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
        private readonly IHubContext<SuperAdminLiveHub> _hubContext;
        private readonly ILiveBroadcastStateService _stateService;
        private readonly ILogger<TikTokIntegrationManager> _logger;
        private TikTokLiveClient? _client;

        public TikTokIntegrationManager(
            IHubContext<SuperAdminLiveHub> hubContext,
            ILiveBroadcastStateService stateService,
            ILogger<TikTokIntegrationManager> logger)
        {
            _hubContext = hubContext;
            _stateService = stateService;
            _logger = logger;
        }

        public async Task ConnectAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            try
            {
                await DisconnectAsync(); // Ensure any existing connection is closed

                _logger.LogInformation("Connecting to TikTok Live for username: {Username}", username);

                _client = new TikTokLiveClient(username);

                _client.OnCommentRecieved += Client_OnCommentRecieved;
                _client.OnLikesRecieved += Client_OnLikesRecieved;
                _client.OnViewerCountUpdated += Client_OnViewerCountUpdated;

                _ = Task.Run(() => 
                {
                    try
                    {
                        _client.Run(null);
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
        }

        public Task DisconnectAsync()
        {
            if (_client != null)
            {
                try
                {
                    _logger.LogInformation("Disconnecting from TikTok Live.");
                    _client.OnCommentRecieved -= Client_OnCommentRecieved;
                    _client.OnLikesRecieved -= Client_OnLikesRecieved;
                    _client.OnViewerCountUpdated -= Client_OnViewerCountUpdated;
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

        private async void Client_OnViewerCountUpdated(object? sender, TikTokLiveSharp.Models.WebcastRoomUserSeqMessage e)
        {
            try
            {
                var state = _stateService.GetState();
                _stateService.UpdateTikTokStats(e.viewerCount, state.TotalLikes);
                await _hubContext.Clients.All.SendAsync("ReceiveTikTokStats", new
                {
                    viewerCount = e.viewerCount,
                    totalLikes = state.TotalLikes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ViewerCountUpdated.");
            }
        }

        private async void Client_OnLikesRecieved(object? sender, TikTokLiveSharp.Models.WebcastLikeMessage e)
        {
            try
            {
                var state = _stateService.GetState();
                var newTotal = state.TotalLikes + e.likeCount;
                _stateService.UpdateTikTokStats(state.ViewerCount, newTotal);
                await _hubContext.Clients.All.SendAsync("ReceiveTikTokStats", new
                {
                    viewerCount = state.ViewerCount,
                    totalLikes = newTotal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling LikesRecieved.");
            }
        }

        private async void Client_OnCommentRecieved(object? sender, TikTokLiveSharp.Models.WebcastChatMessage e)
        {
            try
            {
                var dto = new TikTokCommentDto
                {
                    Username = e.User.uniqueId,
                    Comment = e.Comment,
                    UserAvatarUrl = e.User.profilePicture?.Urls?.FirstOrDefault() ?? "",
                    Timestamp = DateTimeOffset.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("ReceiveTikTokComment", dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling CommentRecieved.");
            }
        }
    }
}
