using AuraNova.Application.Products.DTOs;
using AuraNova.Application.Products.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using AuraNova.Infrastructure.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuraNova.UnitTests
{
    public class ProductServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private ILogger<ProductService> GetMockLogger()
        {
            return new Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductService>();
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateProductSuccessfully()
        {
            using var db = GetInMemoryDb();
            var service = new ProductService(db, GetMockLogger());

            var request = new CreateProductRequest
            {
                Name = "Producto Test",
                Description = "Descripción",
                Price = 99.99m,
                ImageUrl = "https://example.com/img.jpg",
                Stock = 10
            };

            var result = await service.CreateAsync(request);

            Assert.NotNull(result);
            Assert.Equal("Producto Test", result.Name);
            Assert.Equal(99.99m, result.Price);
            Assert.True(result.IsAvailable);
            Assert.Equal(10, result.Stock);
        }

        [Fact]
        public async Task GetAdminProductsAsync_ShouldReturnAllProducts()
        {
            using var db = GetInMemoryDb();
            var service = new ProductService(db, GetMockLogger());

            var p1 = new CreateProductRequest { Name = "Prod1", Price = 10, Stock = 5 };
            var p2 = new CreateProductRequest { Name = "Prod2", Price = 20, Stock = 10 };

            await service.CreateAsync(p1);
            await service.CreateAsync(p2);

            var result = await service.GetAdminProductsAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetPublicProductsAsync_ShouldReturnOnlyAvailableProducts()
        {
            using var db = GetInMemoryDb();
            var service = new ProductService(db, GetMockLogger());

            var p1 = new CreateProductRequest { Name = "Prod1", Price = 10, Stock = 5 };
            var p2 = new CreateProductRequest { Name = "Prod2", Price = 20, Stock = 10 };

            var prod1 = await service.CreateAsync(p1);
            var prod2 = await service.CreateAsync(p2);

            await service.UpdateAvailabilityAsync(prod2.Id, false);

            var result = await service.GetPublicProductsAsync();

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Prod1", result[0].Name);
        }

        [Fact]
        public async Task GetPublicByIdAsync_ShouldReturnNullIfNotAvailable()
        {
            using var db = GetInMemoryDb();
            var service = new ProductService(db, GetMockLogger());

            var request = new CreateProductRequest { Name = "Prod", Price = 10, Stock = 5 };
            var product = await service.CreateAsync(request);

            await service.UpdateAvailabilityAsync(product.Id, false);

            var result = await service.GetPublicByIdAsync(product.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateStockAsync_ShouldUpdateStockCorrectly()
        {
            using var db = GetInMemoryDb();
            var service = new ProductService(db, GetMockLogger());

            var request = new CreateProductRequest { Name = "Prod", Price = 10, Stock = 5 };
            var product = await service.CreateAsync(request);

            var result = await service.UpdateStockAsync(product.Id, 20);

            Assert.True(result);
            var updated = await service.GetAdminByIdAsync(product.Id);
            Assert.Equal(20, updated?.Stock);
        }

        [Fact]
        public async Task UpdateAvailabilityAsync_ShouldToggleAvailability()
        {
            using var db = GetInMemoryDb();
            var service = new ProductService(db, GetMockLogger());

            var request = new CreateProductRequest { Name = "Prod", Price = 10, Stock = 5 };
            var product = await service.CreateAsync(request);

            Assert.True(product.IsAvailable);

            await service.UpdateAvailabilityAsync(product.Id, false);
            var result = await service.GetAdminByIdAsync(product.Id);

            Assert.False(result?.IsAvailable);
        }
    }
}
