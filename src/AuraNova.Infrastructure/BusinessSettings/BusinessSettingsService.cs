using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.DTOs;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.BusinessSettings
{
    public class BusinessSettingsService : IBusinessSettingsService
    {
        private readonly AppDbContext _db;
        private readonly IFileStorageService _storageService;

        public BusinessSettingsService(AppDbContext db, IFileStorageService storageService)
        {
            _db = db;
            _storageService = storageService;
        }

        private async Task<AuraNova.Domain.Entities.BusinessSettings> GetOrCreateSettingsAsync()
        {
            var settings = await _db.BusinessSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new AuraNova.Domain.Entities.BusinessSettings
                {
                    BusinessName = "Aura Nova",
                    WhatsAppNumber = "",
                    YapeHolderName = "",
                    TrackingBaseUrl = "",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.BusinessSettings.Add(settings);
                await _db.SaveChangesAsync();
            }
            return settings;
        }

        public async Task<BusinessSettingsResponse> GetAdminAsync()
        {
            var settings = await GetOrCreateSettingsAsync();
            return MapToResponse(settings);
        }

        public async Task<BusinessSettingsResponse> GetPublicAsync()
        {
            var settings = await GetOrCreateSettingsAsync();
            return MapToResponse(settings);
        }

        public async Task<BusinessSettingsResponse> UpdateAsync(UpdateBusinessSettingsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BusinessName))
                throw new ArgumentException("BusinessName is required.");
            if (string.IsNullOrWhiteSpace(request.WhatsAppNumber))
                throw new ArgumentException("WhatsAppNumber is required.");
            if (string.IsNullOrWhiteSpace(request.YapeHolderName))
                throw new ArgumentException("YapeHolderName is required.");
            if (string.IsNullOrWhiteSpace(request.TrackingBaseUrl))
                throw new ArgumentException("TrackingBaseUrl is required.");

            // Normalize tracking URL (no trailing slash)
            var trackingUrl = request.TrackingBaseUrl.TrimEnd('/');
            if (!Uri.TryCreate(trackingUrl, UriKind.Absolute, out _))
                throw new ArgumentException("TrackingBaseUrl must be a valid URL.");

            // Normalize WhatsApp
            var normalizedPhone = new string(request.WhatsAppNumber.Where(char.IsDigit).ToArray());
            if (normalizedPhone.Length == 9 && normalizedPhone.StartsWith('9'))
                normalizedPhone = "51" + normalizedPhone; // Convert to Peru format if needed
            
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                throw new ArgumentException("Invalid WhatsApp number format.");

            var settings = await GetOrCreateSettingsAsync();
            settings.BusinessName = request.BusinessName.Trim();
            settings.WhatsAppNumber = normalizedPhone;
            settings.YapeHolderName = request.YapeHolderName.Trim();
            settings.TrackingBaseUrl = trackingUrl;
            settings.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            return MapToResponse(settings);
        }

        public async Task<BusinessSettingsResponse> UploadYapeQrAsync(Stream fileStream, string fileName, string contentType)
        {
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedMimeTypes.Contains(contentType.ToLower()))
                throw new ArgumentException("Invalid file type. Only JPG, PNG, and WEBP are allowed.");

            if (fileStream.Length > 5 * 1024 * 1024)
                throw new ArgumentException("File size exceeds 5MB limit.");

            var settings = await GetOrCreateSettingsAsync();
            var oldPath = settings.YapeQrImageUrl;

            var safeFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            
            // Upload to Supabase Storage
            var newUrl = await _storageService.UploadAsync(fileStream, safeFileName, contentType, "business-settings/yape-qr");

            settings.YapeQrImageUrl = newUrl;
            settings.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            // Try delete old QR if exists
            if (!string.IsNullOrWhiteSpace(oldPath))
            {
                try
                {
                    // oldPath could be the full URL, the IFileStorageService implementation 
                    // should be able to extract the path or handle it.
                    await _storageService.DeleteAsync(oldPath);
                }
                catch
                {
                    // Log error but do not fail the upload
                }
            }

            return MapToResponse(settings);
        }

        public async Task RemoveYapeQrAsync()
        {
            var settings = await GetOrCreateSettingsAsync();
            if (!string.IsNullOrWhiteSpace(settings.YapeQrImageUrl))
            {
                var oldPath = settings.YapeQrImageUrl;
                settings.YapeQrImageUrl = null;
                settings.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();

                try
                {
                    await _storageService.DeleteAsync(oldPath);
                }
                catch
                {
                    // Ignore
                }
            }
        }

        private BusinessSettingsResponse MapToResponse(AuraNova.Domain.Entities.BusinessSettings settings)
        {
            return new BusinessSettingsResponse
            {
                Id = settings.Id,
                BusinessName = settings.BusinessName,
                WhatsAppNumber = settings.WhatsAppNumber,
                YapeHolderName = settings.YapeHolderName,
                YapeQrImageUrl = settings.YapeQrImageUrl,
                TrackingBaseUrl = settings.TrackingBaseUrl,
                UpdatedAt = settings.UpdatedAt
            };
        }
    }
}
