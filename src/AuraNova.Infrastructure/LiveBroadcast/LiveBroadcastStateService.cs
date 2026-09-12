using System;
using AuraNova.Application.LiveBroadcast.Interfaces;

namespace AuraNova.Infrastructure.LiveBroadcast
{
    public class LiveBroadcastStateService : ILiveBroadcastStateService
    {
        private readonly object _lock = new();
        private readonly LiveBroadcastStateDto _state = new();

        public LiveBroadcastStateDto GetState()
        {
            lock (_lock)
            {
                return new LiveBroadcastStateDto
                {
                    CurrentLiveText = _state.CurrentLiveText,
                    IsLiveTextActive = _state.IsLiveTextActive,
                    IsTikTokLiveActive = _state.IsTikTokLiveActive,
                    TikTokUsername = _state.TikTokUsername,
                    ViewerCount = _state.ViewerCount,
                    TotalLikes = _state.TotalLikes,
                    UpdatedAt = _state.UpdatedAt
                };
            }
        }

        public void ToggleLiveText(bool isActive)
        {
            lock (_lock)
            {
                _state.IsLiveTextActive = isActive;
                if (!isActive)
                {
                    _state.CurrentLiveText = null;
                }
                _state.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void UpdateLiveText(string? text)
        {
            lock (_lock)
            {
                _state.CurrentLiveText = text;
                _state.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void SetTikTokLiveState(bool isActive, string? username)
        {
            lock (_lock)
            {
                _state.IsTikTokLiveActive = isActive;
                _state.TikTokUsername = isActive ? username : null;
                if (!isActive)
                {
                    _state.ViewerCount = 0;
                    _state.TotalLikes = 0;
                }
                _state.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void UpdateTikTokStats(int viewerCount, long totalLikes)
        {
            lock (_lock)
            {
                _state.ViewerCount = viewerCount;
                _state.TotalLikes = totalLikes;
                _state.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}
