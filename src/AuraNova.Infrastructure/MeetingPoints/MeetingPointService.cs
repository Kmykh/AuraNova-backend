using AuraNova.Application.MeetingPoints.DTOs;
using AuraNova.Application.MeetingPoints.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.MeetingPoints
{
    public class MeetingPointService : IMeetingPointService
    {
        private readonly AppDbContext _db;

        public MeetingPointService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<MeetingPointResponse> CreateAsync(CreateMeetingPointRequest request)
        {
            var point = new MeetingPoint
            {
                Name = request.Name.Trim(),
                Address = request.Address.Trim(),
                Cost = request.Cost
            };

            _db.MeetingPoints.Add(point);
            await _db.SaveChangesAsync();

            return MapToResponse(point);
        }

        public async Task<IReadOnlyList<MeetingPointResponse>> GetAllAsync()
        {
            var points = await _db.MeetingPoints
                .OrderBy(p => p.Name)
                .ToListAsync();

            return points.Select(MapToResponse).ToList();
        }

        public async Task<MeetingPointResponse?> GetByIdAsync(Guid id)
        {
            var point = await _db.MeetingPoints.FindAsync(id);
            return point == null ? null : MapToResponse(point);
        }

        public async Task<MeetingPointResponse?> UpdateAsync(Guid id, UpdateMeetingPointRequest request)
        {
            var point = await _db.MeetingPoints.FindAsync(id);
            if (point == null) return null;

            point.Name = request.Name.Trim();
            point.Address = request.Address.Trim();
            point.Cost = request.Cost;
            point.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            return MapToResponse(point);
        }

        public async Task<bool> UpdateAvailabilityAsync(Guid id, bool isActive)
        {
            var point = await _db.MeetingPoints.FindAsync(id);
            if (point == null) return false;

            point.IsActive = isActive;
            point.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<MeetingPointResponse>> GetPublicAsync()
        {
            var points = await _db.MeetingPoints
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return points.Select(MapToResponse).ToList();
        }

        private static MeetingPointResponse MapToResponse(MeetingPoint point) => new()
        {
            Id = point.Id,
            Name = point.Name,
            Address = point.Address,
            Cost = point.Cost,
            IsActive = point.IsActive,
            CreatedAt = point.CreatedAt,
            UpdatedAt = point.UpdatedAt
        };
    }
}
