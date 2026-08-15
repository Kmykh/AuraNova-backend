using AuraNova.Application.Products.DTOs;

namespace AuraNova.Application.Products.Interfaces
{
    public interface IProductService
    {
        Task<ProductResponse> CreateAsync(CreateProductRequest request);
        
        Task<IReadOnlyList<ProductResponse>> GetAdminProductsAsync();
        
        Task<ProductResponse?> GetAdminByIdAsync(Guid id);
        
        Task<ProductResponse?> UpdateAsync(Guid id, UpdateProductRequest request);
        
        Task<bool> UpdateStockAsync(Guid id, int stock);
        
        Task<bool> UpdateAvailabilityAsync(Guid id, bool isAvailable);
        
        Task<IReadOnlyList<ProductResponse>> GetPublicProductsAsync();
        
        Task<ProductResponse?> GetPublicByIdAsync(Guid id);
    }
}
