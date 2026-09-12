using System;

namespace AuraNova.Application.LiveBroadcast.Interfaces
{
    public class LiveBroadcastStateDto
    {
        public string? CurrentLiveText { get; set; }
        public bool IsTikTokLiveActive { get; set; }
        public string? TikTokUsername { get; set; }
        public int ViewerCount { get; set; }
        public long TotalLikes { get; set; }
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class TikTokCommentDto
    {
        public string Username { get; set; } = null!;
        public string Comment { get; set; } = null!;
        public string? UserAvatarUrl { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    public interface ILiveBroadcastStateService
    {
        LiveBroadcastStateDto GetState();
        void UpdateLiveText(string? text);
        void SetTikTokLiveState(bool isActive, string? username);
        void UpdateTikTokStats(int viewerCount, long totalLikes);
    }
}
