using AuraNova.Application.DeliveryZones.DTOs;
using AuraNova.Application.DeliveryZones.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.DeliveryZones
{
    public class DeliveryZoneService : IDeliveryZoneService
    {
        private readonly AppDbContext _db;

        public DeliveryZoneService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DeliveryZoneResponse> CreateAsync(CreateDeliveryZoneRequest request)
        {
            var zone = new DeliveryZone
            {
                Name = request.Name.Trim(),
                District = request.District.Trim(),
                Cost = request.Cost
            };

            _db.DeliveryZones.Add(zone);
            await _db.SaveChangesAsync();

            return MapToResponse(zone);
        }

        public async Task<IReadOnlyList<DeliveryZoneResponse>> GetAllAsync()
        {
            var zones = await _db.DeliveryZones
                .OrderBy(z => z.Name)
                .ToListAsync();

            return zones.Select(MapToResponse).ToList();
        }

        public async Task<DeliveryZoneResponse?> GetByIdAsync(Guid id)
        {
            var zone = await _db.DeliveryZones.FindAsync(id);
            return zone == null ? null : MapToResponse(zone);
        }

        public async Task<DeliveryZoneResponse?> UpdateAsync(Guid id, UpdateDeliveryZoneRequest request)
        {
            var zone = await _db.DeliveryZones.FindAsync(id);
            if (zone == null) return null;

            zone.Name = request.Name.Trim();
            zone.District = request.District.Trim();
            zone.Cost = request.Cost;
            zone.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            return MapToResponse(zone);
        }

        public async Task<bool> UpdateAvailabilityAsync(Guid id, bool isActive)
        {
            var zone = await _db.DeliveryZones.FindAsync(id);
            if (zone == null) return false;

            zone.IsActive = isActive;
            zone.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<DeliveryZoneResponse>> GetPublicAsync()
        {
            var zones = await _db.DeliveryZones
                .Where(z => z.IsActive)
                .OrderBy(z => z.Name)
                .ToListAsync();

            return zones.Select(MapToResponse).ToList();
        }

        private static DeliveryZoneResponse MapToResponse(DeliveryZone zone) => new()
        {
            Id = zone.Id,
            Name = zone.Name,
            District = zone.District,
            Cost = zone.Cost,
            IsActive = zone.IsActive,
            CreatedAt = zone.CreatedAt,
            UpdatedAt = zone.UpdatedAt
        };
    }
}
