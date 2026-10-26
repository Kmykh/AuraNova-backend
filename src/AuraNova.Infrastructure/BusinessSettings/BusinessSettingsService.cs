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

            // Normalize WhatsApp
            var normalizedPhone = new string(request.WhatsAppNumber.Where(char.IsDigit).ToArray());
            if (normalizedPhone.Length == 9 && normalizedPhone.StartsWith('9'))
                normalizedPhone = "51" + normalizedPhone; // Convert to Peru format if needed
            
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                throw new ArgumentException("Invalid WhatsApp number format.");

            var settings = await GetOrCreateSettingsAsync();
            settings.BusinessName = request.BusinessName.Trim();
            settings.WhatsAppNumber = normalizedPhone;
            // Solo del cobro antiguo por QR de Yape: ya no se pide en el panel y, si no llega, se mantiene
            if (!string.IsNullOrWhiteSpace(request.YapeHolderName))
                settings.YapeHolderName = request.YapeHolderName.Trim();
            settings.TikTokUsername = request.TikTokUsername?.Trim();
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

        // Paleta inicial: la que usaba el panel antes de que fuera editable
        private static readonly ColorOptionDto[] DefaultColors =
        [
            new() { Name = "Rojo", Hex = "#ef4444" },
            new() { Name = "Rosado", Hex = "#f472b6" },
            new() { Name = "Rosa Pastel", Hex = "#fbcfe8" },
            new() { Name = "Blanco", Hex = "#ffffff" },
            new() { Name = "Amarillo", Hex = "#facc15" },
            new() { Name = "Azul", Hex = "#3b82f6" },
            new() { Name = "Celeste", Hex = "#93c5fd" },
            new() { Name = "Morado", Hex = "#8b5cf6" },
            new() { Name = "Lila", Hex = "#c4b5fd" },
            new() { Name = "Fucsia", Hex = "#d946ef" },
            new() { Name = "Naranja", Hex = "#fb923c" },
            new() { Name = "Champagne", Hex = "#fef3c7" },
            new() { Name = "Vino Tinto", Hex = "#881337" },
            new() { Name = "Multicolor", Hex = MulticolorHex },
        ];

        private static readonly string[] DefaultFlowerTypes =
        [
            "Rosas", "Girasoles", "Tulipanes", "Lirios", "Orquídeas", "Margaritas", "Claveles", "Hortensias",
            "Peonías", "Crisantemos", "Gerberas", "Gypsophila (Lluvia)", "Astromelias", "Lavanda", "Flores Silvestres"
        ];

        private const string MulticolorHex = "multicolor";
        private const int MaxColors = 40;
        private const int MaxFlowerTypes = 40;
        private const int MaxOptionLength = 40;

        public async Task<CustomizationOptionsDto> GetCustomizationOptionsAsync()
        {
            var settings = await GetOrCreateSettingsAsync();
            return MapOptions(settings);
        }

        public async Task<CustomizationOptionsDto> UpdateCustomizationOptionsAsync(CustomizationOptionsDto request)
        {
            var colors = new List<ColorOption>();
            foreach (var color in request.Colors ?? [])
            {
                var name = color.Name?.Trim() ?? string.Empty;
                var hex = color.Hex?.Trim().ToLowerInvariant() ?? string.Empty;
                if (name.Length == 0)
                    continue;
                if (name.Length > MaxOptionLength)
                    throw new ArgumentException($"El color '{name}' supera los {MaxOptionLength} caracteres.");
                if (name.Contains(','))
                    throw new ArgumentException($"El color '{name}' no puede tener comas: agrégalo como opciones separadas.");
                if (hex != MulticolorHex && !System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9a-f]{6}$"))
                    throw new ArgumentException($"El código del color '{name}' no es válido (usa #rrggbb).");
                if (colors.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                colors.Add(new ColorOption { Name = name, Hex = hex });
            }

            var flowers = new List<string>();
            foreach (var flower in request.FlowerTypes ?? [])
            {
                var name = flower?.Trim() ?? string.Empty;
                if (name.Length == 0)
                    continue;
                if (name.Length > MaxOptionLength)
                    throw new ArgumentException($"La flor '{name}' supera los {MaxOptionLength} caracteres.");
                if (name.Contains(','))
                    throw new ArgumentException($"La flor '{name}' no puede tener comas: agrégala como opciones separadas.");
                if (!flowers.Contains(name, StringComparer.OrdinalIgnoreCase))
                    flowers.Add(name);
            }

            if (colors.Count > MaxColors || flowers.Count > MaxFlowerTypes)
                throw new ArgumentException($"Puedes tener hasta {MaxColors} colores y {MaxFlowerTypes} tipos de flor.");

            var settings = await GetOrCreateSettingsAsync();
            settings.ColorPalette = colors;
            settings.FlowerTypeOptions = flowers;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            return MapOptions(settings);
        }

        private static CustomizationOptionsDto MapOptions(AuraNova.Domain.Entities.BusinessSettings settings) => new()
        {
            Colors = settings.ColorPalette is { Count: > 0 }
                ? settings.ColorPalette.Select(c => new ColorOptionDto { Name = c.Name, Hex = c.Hex }).ToList()
                : DefaultColors.Select(c => new ColorOptionDto { Name = c.Name, Hex = c.Hex }).ToList(),
            FlowerTypes = settings.FlowerTypeOptions is { Count: > 0 }
                ? settings.FlowerTypeOptions.ToList()
                : DefaultFlowerTypes.ToList()
        };

        private BusinessSettingsResponse MapToResponse(AuraNova.Domain.Entities.BusinessSettings settings)
        {
            return new BusinessSettingsResponse
            {
                Id = settings.Id,
                BusinessName = settings.BusinessName,
                WhatsAppNumber = settings.WhatsAppNumber,
                YapeHolderName = settings.YapeHolderName,
                YapeQrImageUrl = settings.YapeQrImageUrl,
                TikTokUsername = settings.TikTokUsername,
                UpdatedAt = settings.UpdatedAt
            };
        }
    }
}
