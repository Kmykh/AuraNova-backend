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
        private readonly ILogger<ProductService> _logger;

        public ProductService(AppDbContext db, ILogger<ProductService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
        {
            var product = new Product
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Price = request.Price,
                ImageUrl = request.ImageUrl?.Trim(),
                Stock = request.Stock,
                IsAvailable = true
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Producto creado: {ProductId} - {ProductName}", product.Id, product.Name);

            return MapToResponse(product);
        }

        public async Task<IReadOnlyList<ProductResponse>> GetAdminProductsAsync()
        {
            var products = await _db.Products
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(MapToResponse).ToList().AsReadOnly();
        }

        public async Task<ProductResponse?> GetAdminByIdAsync(Guid id)
        {
            var product = await _db.Products.FindAsync(id);
            return product == null ? null : MapToResponse(product);
        }

        public async Task<ProductResponse?> UpdateAsync(Guid id, UpdateProductRequest request)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return null;

            product.Name = request.Name.Trim();
            product.Description = request.Description?.Trim();
            product.Price = request.Price;
            product.ImageUrl = request.ImageUrl?.Trim();
            product.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Products.Update(product);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Producto actualizado: {ProductId}", id);

            return MapToResponse(product);
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

            _logger.LogInformation("Disponibilidad actualizada: {ProductId} - IsAvailable: {IsAvailable}", id, isAvailable);

            return true;
        }

        public async Task<IReadOnlyList<ProductResponse>> GetPublicProductsAsync()
        {
            var products = await _db.Products
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(MapToResponse).ToList().AsReadOnly();
        }

        public async Task<ProductResponse?> GetPublicByIdAsync(Guid id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null || !product.IsAvailable)
                return null;

            return MapToResponse(product);
        }

        private static ProductResponse MapToResponse(Product product)
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
                UpdatedAt = product.UpdatedAt
            };
        }
    }
}
