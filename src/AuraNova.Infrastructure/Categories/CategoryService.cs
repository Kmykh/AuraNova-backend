using AuraNova.Application.Categories.DTOs;
using AuraNova.Application.Categories.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Categories
{
    public class CategoryService(AppDbContext db, ILogger<CategoryService> logger) : ICategoryService
    {
        public async Task<CategoryResponse?> CreateAsync(CreateCategoryRequest request)
        {
            if (await db.Categories.AnyAsync(c => c.Slug == request.Slug))
            {
                return null; // Slug duplicated
            }

            var category = new Category
            {
                Name = request.Name.Trim(),
                Slug = request.Slug.Trim(),
                Description = request.Description?.Trim(),
                IsActive = request.IsActive
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync();

            logger.LogInformation("Categoría creada: {CategoryId} - {CategoryName}", category.Id, category.Name);

            return MapToResponse(category);
        }

        public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(bool includeInactive = false)
        {
            var query = db.Categories.AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            var categories = await query.OrderBy(c => c.Name).ToListAsync();
            return categories.Select(MapToResponse).ToList().AsReadOnly();
        }

        public async Task<CategoryResponse?> GetByIdAsync(Guid id)
        {
            var category = await db.Categories.FindAsync(id);
            return category == null ? null : MapToResponse(category);
        }

        public async Task<CategoryResponse?> UpdateAsync(Guid id, UpdateCategoryRequest request)
        {
            var category = await db.Categories.FindAsync(id);
            if (category == null)
                return null;

            if (category.Slug != request.Slug && await db.Categories.AnyAsync(c => c.Slug == request.Slug))
            {
                return null; // Slug duplicated
            }

            category.Name = request.Name.Trim();
            category.Slug = request.Slug.Trim();
            category.Description = request.Description?.Trim();
            category.UpdatedAt = DateTimeOffset.UtcNow;

            db.Categories.Update(category);
            await db.SaveChangesAsync();

            logger.LogInformation("Categoría actualizada: {CategoryId}", id);

            return MapToResponse(category);
        }

        public async Task<bool> UpdateStatusAsync(Guid id, bool isActive)
        {
            var category = await db.Categories.FindAsync(id);
            if (category == null)
                return false;

            category.IsActive = isActive;
            category.UpdatedAt = DateTimeOffset.UtcNow;

            db.Categories.Update(category);
            await db.SaveChangesAsync();

            logger.LogInformation("Estado de categoría actualizado: {CategoryId} - IsActive: {IsActive}", id, isActive);

            return true;
        }

        private static CategoryResponse MapToResponse(Category category)
        {
            return new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            };
        }
    }
}
