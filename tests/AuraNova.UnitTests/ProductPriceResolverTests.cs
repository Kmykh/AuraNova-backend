using System;
using System.Threading.Tasks;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Orders;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraNova.UnitTests
{
    public class ProductPriceResolverTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task ResolvePriceAsync_NoCampaign_ReturnsBasePrice()
        {
            // Arrange
            using var db = GetInMemoryDb();
            var product = new Product { Id = Guid.NewGuid(), Name = "Test", Price = 50m, IsAvailable = true, Stock = 10 };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var resolver = new ProductPriceResolver(db, new NullLogger<ProductPriceResolver>());

            // Act
            var result = await resolver.ResolvePriceAsync(product.Id);

            // Assert
            Assert.False(result.IsCampaignPrice);
            Assert.Equal(50m, result.EffectivePrice);
            Assert.Equal(50m, result.ProductBasePrice);
            Assert.Null(result.CampaignId);
        }

        [Fact]
        public async Task ResolvePriceAsync_ActiveCampaign_ReturnsCampaignPrice()
        {
            // Arrange
            using var db = GetInMemoryDb();
            var product = new Product { Id = Guid.NewGuid(), Name = "Test", Price = 50m, IsAvailable = true, Stock = 10 };
            db.Products.Add(product);
            
            var campaign = new Campaign 
            { 
                Id = Guid.NewGuid(), 
                Name = "Flores Amarillas", 
                StartDate = DateTimeOffset.UtcNow.AddDays(-1), 
                EndDate = DateTimeOffset.UtcNow.AddDays(10), 
                IsActive = true 
            };
            db.Campaigns.Add(campaign);
            
            var stage = new CampaignStage 
            { 
                Id = Guid.NewGuid(), 
                CampaignId = campaign.Id, 
                Name = "Preventa", 
                StartDate = DateTimeOffset.UtcNow.AddDays(-1), 
                EndDate = DateTimeOffset.UtcNow.AddDays(10), 
                IsActive = true 
            };
            db.CampaignStages.Add(stage);

            var campaignProduct = new CampaignProduct
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                ProductId = product.Id,
                IsActive = true
            };
            db.CampaignProducts.Add(campaignProduct);

            var stagePrice = new CampaignProductStagePrice
            {
                Id = Guid.NewGuid(),
                CampaignProductId = campaignProduct.Id,
                CampaignStageId = stage.Id,
                Price = 42m
            };
            db.CampaignProductStagePrices.Add(stagePrice);

            await db.SaveChangesAsync();

            var resolver = new ProductPriceResolver(db, new NullLogger<ProductPriceResolver>());

            // Act
            var result = await resolver.ResolvePriceAsync(product.Id);

            // Assert
            Assert.True(result.IsCampaignPrice);
            Assert.Equal(42m, result.EffectivePrice);
            Assert.Equal(50m, result.ProductBasePrice);
            Assert.Equal(campaign.Id, result.CampaignId);
            Assert.Equal(stage.Id, result.CampaignStageId);
        }

        [Fact]
        public async Task ResolvePriceAsync_ExpiredCampaign_ReturnsBasePrice()
        {
            // Arrange
            using var db = GetInMemoryDb();
            var product = new Product { Id = Guid.NewGuid(), Name = "Test", Price = 50m, IsAvailable = true, Stock = 10 };
            db.Products.Add(product);
            
            // Campaign ended yesterday
            var campaign = new Campaign 
            { 
                Id = Guid.NewGuid(), 
                Name = "Flores Amarillas", 
                StartDate = DateTimeOffset.UtcNow.AddDays(-10), 
                EndDate = DateTimeOffset.UtcNow.AddDays(-1), 
                IsActive = true 
            };
            db.Campaigns.Add(campaign);
            
            var stage = new CampaignStage 
            { 
                Id = Guid.NewGuid(), 
                CampaignId = campaign.Id, 
                Name = "Preventa", 
                StartDate = DateTimeOffset.UtcNow.AddDays(-10), 
                EndDate = DateTimeOffset.UtcNow.AddDays(-1), 
                IsActive = true 
            };
            db.CampaignStages.Add(stage);

            var campaignProduct = new CampaignProduct
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                ProductId = product.Id,
                IsActive = true
            };
            db.CampaignProducts.Add(campaignProduct);

            var stagePrice = new CampaignProductStagePrice
            {
                Id = Guid.NewGuid(),
                CampaignProductId = campaignProduct.Id,
                CampaignStageId = stage.Id,
                Price = 42m
            };
            db.CampaignProductStagePrices.Add(stagePrice);

            await db.SaveChangesAsync();

            var resolver = new ProductPriceResolver(db, new NullLogger<ProductPriceResolver>());

            // Act
            var result = await resolver.ResolvePriceAsync(product.Id);

            // Assert
            Assert.False(result.IsCampaignPrice);
            Assert.Equal(50m, result.EffectivePrice);
        }
        
        [Fact]
        public async Task ResolvePriceAsync_InactiveStage_ReturnsBasePrice()
        {
            // Arrange
            using var db = GetInMemoryDb();
            var product = new Product { Id = Guid.NewGuid(), Name = "Test", Price = 50m, IsAvailable = true, Stock = 10 };
            db.Products.Add(product);
            
            var campaign = new Campaign 
            { 
                Id = Guid.NewGuid(), 
                Name = "Flores Amarillas", 
                StartDate = DateTimeOffset.UtcNow.AddDays(-1), 
                EndDate = DateTimeOffset.UtcNow.AddDays(10), 
                IsActive = true 
            };
            db.Campaigns.Add(campaign);
            
            var stage = new CampaignStage 
            { 
                Id = Guid.NewGuid(), 
                CampaignId = campaign.Id, 
                Name = "Preventa", 
                StartDate = DateTimeOffset.UtcNow.AddDays(-1), 
                EndDate = DateTimeOffset.UtcNow.AddDays(10), 
                IsActive = false // Stage is inactive
            };
            db.CampaignStages.Add(stage);

            var campaignProduct = new CampaignProduct
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                ProductId = product.Id,
                IsActive = true
            };
            db.CampaignProducts.Add(campaignProduct);

            var stagePrice = new CampaignProductStagePrice
            {
                Id = Guid.NewGuid(),
                CampaignProductId = campaignProduct.Id,
                CampaignStageId = stage.Id,
                Price = 42m
            };
            db.CampaignProductStagePrices.Add(stagePrice);

            await db.SaveChangesAsync();

            var resolver = new ProductPriceResolver(db, new NullLogger<ProductPriceResolver>());

            // Act
            var result = await resolver.ResolvePriceAsync(product.Id);

            // Assert
            Assert.False(result.IsCampaignPrice);
            Assert.Equal(50m, result.EffectivePrice);
        }
    }
}
