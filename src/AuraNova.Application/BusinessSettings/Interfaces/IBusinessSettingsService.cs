using System.IO;
using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.DTOs;

namespace AuraNova.Application.BusinessSettings.Interfaces
{
    public interface IBusinessSettingsService
    {
        Task<BusinessSettingsResponse> GetPublicAsync();
        Task<BusinessSettingsResponse> GetAdminAsync();
        Task<BusinessSettingsResponse> UpdateAsync(UpdateBusinessSettingsRequest request);
        Task<BusinessSettingsResponse> UploadYapeQrAsync(Stream fileStream, string fileName, string contentType);
        Task RemoveYapeQrAsync();
    }
}
