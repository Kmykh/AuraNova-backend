using AuraNova.Application.DeliveryZones.DTOs;

namespace AuraNova.Application.DeliveryZones.Interfaces
{
    public interface IDeliveryZoneService
    {
        Task<DeliveryZoneResponse> CreateAsync(CreateDeliveryZoneRequest request);
        Task<IReadOnlyList<DeliveryZoneResponse>> GetAllAsync();
        Task<DeliveryZoneResponse?> GetByIdAsync(Guid id);
        Task<DeliveryZoneResponse?> UpdateAsync(Guid id, UpdateDeliveryZoneRequest request);
        Task<bool> UpdateAvailabilityAsync(Guid id, bool isActive);
        Task<IReadOnlyList<DeliveryZoneResponse>> GetPublicAsync();
    }
}
