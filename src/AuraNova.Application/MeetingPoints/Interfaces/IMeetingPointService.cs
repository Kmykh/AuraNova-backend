using AuraNova.Application.MeetingPoints.DTOs;

namespace AuraNova.Application.MeetingPoints.Interfaces
{
    public interface IMeetingPointService
    {
        Task<MeetingPointResponse> CreateAsync(CreateMeetingPointRequest request);
        Task<IReadOnlyList<MeetingPointResponse>> GetAllAsync();
        Task<MeetingPointResponse?> GetByIdAsync(Guid id);
        Task<MeetingPointResponse?> UpdateAsync(Guid id, UpdateMeetingPointRequest request);
        Task<bool> UpdateAvailabilityAsync(Guid id, bool isActive);
        Task<IReadOnlyList<MeetingPointResponse>> GetPublicAsync();
    }
}
