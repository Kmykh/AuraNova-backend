using AuraNova.Application.Categories.DTOs;
using AuraNova.Application.Products.DTOs;
using AuraNova.Application.Products.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Products
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProductService> _logger; // IDE refresh trigger
        private readonly AuraNova.Application.Orders.Interfaces.IProductPriceResolver _priceResolver;
        private readonly IFileStorageService _storage;

        public ProductService(AppDbContext db, ILogger<ProductService> logger,
            AuraNova.Application.Orders.Interfaces.IProductPriceResolver priceResolver,
            IFileStorageService storage)
        {
            _db = db;
            _logger = logger;
            _priceResolver = priceResolver;
            _storage = storage;
        }

        public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
        {
            if (request.CategoryId.HasValue)
            {
                var category = await _db.Categories.FindAsync(request.CategoryId.Value);
                if (category == null || !category.IsActive)
                {
                    throw new System.Exception("Categoría inválida o inactiva");
                }
            }

            var images = NormalizeImages(request.ImageUrls, request.ImageUrl, existing: null);

            var product = new Product
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Price = request.Price,
                ImageUrl = images.FirstOrDefault(),
                Stock = request.Stock,
                IsAvailable = true,
                AvailableColors = CleanOptions(request.AvailableColors, "color"),
                AvailableFlowerTypes = CleanOptions(request.AvailableFlowerTypes, "tipo de flor"),
                AllowsLights = request.AllowsLights,
                AllowsButterfly = request.AllowsButterfly,
                AllowsPhraseCard = request.AllowsPhraseCard,
                CategoryId = request.CategoryId,
                Audience = request.Audience
            };

            product.Images = images
                .Select((path, index) => new ProductImage { ProductId = product.Id, Path = path, SortOrder = index })
                .ToList();

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Producto creado: {ProductId} - {ProductName}", product.Id, product.Name);

            return MapToResponse(product, null);
        }

        public async Task<IReadOnlyList<ProductResponse>> GetAdminProductsAsync()
        {
            var products = await _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Images)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(p => MapToResponse(p)).ToList().AsReadOnly();
        }

        public async Task<ProductResponse?> GetAdminByIdAsync(Guid id)
        {
            var product = await _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            return product == null ? null : MapToResponse(product);
        }

        public async Task<ProductResponse?> UpdateAsync(Guid id, UpdateProductRequest request)
        {
            var product = await _db.Products.Include(p => p.Category).Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return null;

            if (request.CategoryId.HasValue && request.CategoryId != product.CategoryId)
            {
                var category = await _db.Categories.FindAsync(request.CategoryId.Value);
                if (category == null || !category.IsActive)
                {
                    throw new System.Exception("Categoría inválida o inactiva");
                }
            }

            var previousImages = GalleryPaths(product);
            var images = NormalizeImages(request.ImageUrls, request.ImageUrl, previousImages);

            product.Name = request.Name.Trim();
            product.Description = request.Description?.Trim();
            product.Price = request.Price;
            product.ImageUrl = images.FirstOrDefault();
            product.AvailableColors = CleanOptions(request.AvailableColors, "color");
            product.AvailableFlowerTypes = CleanOptions(request.AvailableFlowerTypes, "tipo de flor");
            product.AllowsLights = request.AllowsLights;
            product.AllowsButterfly = request.AllowsButterfly;
            product.AllowsPhraseCard = request.AllowsPhraseCard;
            product.CategoryId = request.CategoryId;
            product.Audience = request.Audience;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            // La galería se reescribe completa en el orden recibido
            _db.ProductImages.RemoveRange(product.Images ?? []);
            var gallery = images
                .Select((path, index) => new ProductImage { ProductId = product.Id, Path = path, SortOrder = index })
                .ToList();
            _db.ProductImages.AddRange(gallery);
            product.Images = gallery;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Producto actualizado: {ProductId}", id);

            foreach (var removed in previousImages.Except(images))
                await DeleteImageIfUnusedAsync(removed);

            return MapToResponse(product, null);
        }

        public async Task<bool> UpdateStockAsync(Guid id, int stock)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return false;

            product.Stock = stock;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Products.Update(product);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Stock actualizado: {ProductId} - Nueva cantidad: {Stock}", id, stock);

            return true;
        }

        public async Task<bool> UpdateAvailabilityAsync(Guid id, bool isAvailable)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return false;

            product.IsAvailable = isAvailable;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Products.Update(product);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Disponibilidad actualizada: {ProductId} - IsAvailable: {IsAvailable}", id,
                isAvailable);

            return true;
        }

        public async Task<IReadOnlyList<ProductResponse>> GetPublicProductsAsync()
        {
            var products = await _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var prices = await _priceResolver.ResolvePricesAsync(products.Select(p => p.Id).ToList());

            return products
                .Select(p => MapToResponse(p, prices.GetValueOrDefault(p.Id)))
                .ToList()
                .AsReadOnly();
        }

        public async Task<ProductResponse?> GetPublicByIdAsync(Guid id)
        {
            var product = await _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null || !product.IsAvailable)
                return null;

            var priceResult = await _priceResolver.ResolvePriceAsync(product.Id);
            var response = MapToResponse(product, priceResult);

            if (priceResult.IsCampaignPrice && priceResult.CampaignId.HasValue)
            {
                // Todas las etapas de la campaña con el precio de este producto: el detalle muestra cuándo sube
                var campaignId = priceResult.CampaignId.Value;
                response.CampaignStages = await _db.CampaignProductStagePrices
                    .AsNoTracking()
                    .Where(sp => sp.CampaignProduct!.ProductId == product.Id &&
                                 sp.CampaignProduct.CampaignId == campaignId &&
                                 sp.CampaignProduct.IsActive &&
                                 sp.CampaignStage!.IsActive)
                    .OrderBy(sp => sp.CampaignStage!.StartDate)
                    .Select(sp => new ProductCampaignStageResponse
                    {
                        Name = sp.CampaignStage!.Name,
                        StartDate = sp.CampaignStage.StartDate,
                        EndDate = sp.CampaignStage.EndDate,
                        Price = sp.Price,
                        IsCurrent = sp.CampaignStageId == priceResult.CampaignStageId
                    })
                    .ToListAsync();
            }

            return response;
        }

        /// <summary>
        /// Borra del bucket la imagen reemplazada si ya ningún producto la usa (evita archivos huérfanos).
        /// Solo gestiona archivos subidos por el panel (carpeta products/).
        /// </summary>
        private async Task DeleteImageIfUnusedAsync(string imagePath)
        {
            if (!imagePath.StartsWith("products/", StringComparison.OrdinalIgnoreCase))
                return;

            var stillUsed = await _db.Products.AnyAsync(p => p.ImageUrl == imagePath) ||
                            await _db.ProductImages.AnyAsync(i => i.Path == imagePath);
            if (stillUsed)
                return;

            await _storage.DeleteAsync(imagePath);
            _logger.LogInformation("Imagen anterior eliminada del storage: {Path}", imagePath);
        }

        private const int MaxGalleryImages = 8;

        /// <summary>
        /// Galería final en orden. Con <paramref name="imageUrls"/> se usa tal cual; sin ella (clientes anteriores)
        /// la imagen única reemplaza la portada y se conservan las demás fotos.
        /// </summary>
        private static List<string> NormalizeImages(List<string>? imageUrls, string? imageUrl, IReadOnlyList<string>? existing)
        {
            List<string> images;
            if (imageUrls != null)
            {
                images = imageUrls.Select(u => u?.Trim() ?? string.Empty).Where(u => u.Length > 0).Distinct().ToList();
            }
            else
            {
                images = new List<string>();
                if (!string.IsNullOrWhiteSpace(imageUrl))
                    images.Add(imageUrl.Trim());
                images.AddRange((existing ?? []).Skip(1).Where(u => !images.Contains(u)));
            }

            if (images.Count > MaxGalleryImages)
                throw new ArgumentException($"Un producto puede tener hasta {MaxGalleryImages} fotos.");

            foreach (var path in images)
            {
                var isUrl = Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
                if (path.Length > 1000 || (!isUrl && (path.StartsWith('/') || path.Contains("..") || path.Contains(':'))))
                    throw new ArgumentException("Una de las fotos del producto no tiene una ruta válida.");
            }

            return images;
        }

        /// <summary>Cada opción es un elemento: sin comas, sin repetidos y sin espacios sobrantes.</summary>
        private static List<string> CleanOptions(List<string>? options, string label)
        {
            var result = new List<string>();
            foreach (var option in options ?? [])
            {
                var value = option?.Trim() ?? string.Empty;
                if (value.Length == 0 || result.Contains(value, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (value.Contains(','))
                    throw new ArgumentException($"El {label} '{value}' tiene comas: agrega cada opción por separado.");
                if (value.Length > 50)
                    throw new ArgumentException($"El {label} '{value}' supera los 50 caracteres.");
                result.Add(value);
            }
            return result;
        }

        private static List<string> GalleryPaths(Product product)
        {
            var gallery = (product.Images ?? []).OrderBy(i => i.SortOrder).Select(i => i.Path).ToList();
            if (gallery.Count == 0 && !string.IsNullOrWhiteSpace(product.ImageUrl))
                gallery.Add(product.ImageUrl);
            return gallery;
        }

        private static ProductResponse MapToResponse(Product product,
            AuraNova.Application.Orders.Interfaces.ProductPriceResolutionResult? priceResult = null)
        {
            return new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Images = GalleryPaths(product),
                Stock = product.Stock,
                IsAvailable = product.IsAvailable,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                AvailableColors = product.AvailableColors ?? new List<string>(),
                AvailableFlowerTypes = product.AvailableFlowerTypes ?? new List<string>(),
                AllowsLights = product.AllowsLights,
                AllowsButterfly = product.AllowsButterfly,
                AllowsPhraseCard = product.AllowsPhraseCard,
                Audience = product.Audience,
                Category = product.Category != null
                    ? new CategoryResponse
                    {
                        Id = product.Category.Id,
                        Name = product.Category.Name,
                        Slug = product.Category.Slug,
                        Description = product.Category.Description,
                        IsActive = product.Category.IsActive,
                        CreatedAt = product.Category.CreatedAt,
                        UpdatedAt = product.Category.UpdatedAt
                    }
                    : null,

                EffectivePrice = priceResult?.EffectivePrice ?? product.Price,
                IsCampaignActive = priceResult?.IsCampaignPrice ?? false,
                CampaignId = priceResult?.CampaignId,
                CampaignName = priceResult?.CampaignName,
                CampaignStageName = priceResult?.CampaignStageName,
                CampaignStageEndsAt = priceResult?.CampaignStageEndDate
            };
        }
    }
}
