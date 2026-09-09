using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuraNova.Application.Categories.DTOs;

namespace AuraNova.Application.Categories.Interfaces
{
    public interface ICategoryService
    {
        Task<IReadOnlyList<CategoryResponse>> GetAllAsync(bool includeInactive = false);
        Task<CategoryResponse?> GetByIdAsync(Guid id);
        Task<CategoryResponse?> CreateAsync(CreateCategoryRequest request);
        Task<CategoryResponse?> UpdateAsync(Guid id, UpdateCategoryRequest request);
        Task<bool> UpdateStatusAsync(Guid id, bool isActive);
    }
}
