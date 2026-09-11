using AuraNova.Application.Categories.DTOs;
using AuraNova.Application.Products.DTOs;
using AuraNova.Application.Products.Interfaces;
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

        public ProductService(AppDbContext db, ILogger<ProductService> logger,
            AuraNova.Application.Orders.Interfaces.IProductPriceResolver priceResolver)
        {
            _db = db;
            _logger = logger;
            _priceResolver = priceResolver;
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

            var product = new Product
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Price = request.Price,
                ImageUrl = request.ImageUrl?.Trim(),
                Stock = request.Stock,
                IsAvailable = true,
                AvailableColors = request.AvailableColors ?? new List<string>(),
                AvailableFlowerTypes = request.AvailableFlowerTypes ?? new List<string>(),
                AllowsLights = request.AllowsLights,
                AllowsButterfly = request.AllowsButterfly,
                AllowsPhraseCard = request.AllowsPhraseCard,
                CategoryId = request.CategoryId,
                Audience = request.Audience
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Producto creado: {ProductId} - {ProductName}", product.Id, product.Name);

            return MapToResponse(product, null);
        }

        public async Task<IReadOnlyList<ProductResponse>> GetAdminProductsAsync()
        {
            var products = await _db.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(p => MapToResponse(p)).ToList().AsReadOnly();
        }

        public async Task<ProductResponse?> GetAdminByIdAsync(Guid id)
        {
            var product = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
            return product == null ? null : MapToResponse(product);
        }

        public async Task<ProductResponse?> UpdateAsync(Guid id, UpdateProductRequest request)
        {
            var product = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
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

            product.Name = request.Name.Trim();
            product.Description = request.Description?.Trim();
            product.Price = request.Price;
            product.ImageUrl = request.ImageUrl?.Trim();
            product.AvailableColors = request.AvailableColors ?? new List<string>();
            product.AvailableFlowerTypes = request.AvailableFlowerTypes ?? new List<string>();
            product.AllowsLights = request.AllowsLights;
            product.AllowsButterfly = request.AllowsButterfly;
            product.AllowsPhraseCard = request.AllowsPhraseCard;
            product.CategoryId = request.CategoryId;
            product.Audience = request.Audience;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Products.Update(product);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Producto actualizado: {ProductId}", id);

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
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var responses = new List<ProductResponse>();
            foreach (var product in products)
            {
                var priceResult = await _priceResolver.ResolvePriceAsync(product.Id);
                responses.Add(MapToResponse(product, priceResult));
            }

            return responses.AsReadOnly();
        }

        public async Task<ProductResponse?> GetPublicByIdAsync(Guid id)
        {
            var product = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null || !product.IsAvailable)
                return null;

            var priceResult = await _priceResolver.ResolvePriceAsync(product.Id);
            return MapToResponse(product, priceResult);
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
                CampaignStageName = priceResult?.CampaignStageName
            };
        }
    }
}
