using System;

namespace AuraNova.Application.LiveBroadcast.Interfaces
{
    public class LiveBroadcastStateDto
    {
        public string? CurrentLiveText { get; set; }
        public bool IsLiveTextActive { get; set; }
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public interface ILiveBroadcastStateService
    {
        LiveBroadcastStateDto GetState();
        void ToggleLiveText(bool isActive);
        void UpdateLiveText(string? text);
    }
}
