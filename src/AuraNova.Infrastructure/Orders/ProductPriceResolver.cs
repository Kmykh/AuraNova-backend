using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Orders
{
    public class ProductPriceResolver : IProductPriceResolver
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProductPriceResolver> _logger;

        public ProductPriceResolver(AppDbContext db, ILogger<ProductPriceResolver> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ProductPriceResolutionResult> ResolvePriceAsync(Guid productId, DateTimeOffset? currentDate = null)
        {
            var date = currentDate ?? DateTimeOffset.UtcNow;

            // First, get the product base price
            var product = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new { p.Id, p.Price })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                throw new ArgumentException($"Product with Id {productId} not found.");
            }

            var defaultResult = new ProductPriceResolutionResult
            {
                ProductBasePrice = product.Price,
                EffectivePrice = product.Price,
                IsCampaignPrice = false
            };

            // Look for an active campaign stage price for this product and date
            // The logic: 
            // 1. Campaign must be IsActive
            // 2. Campaign StartDate <= date <= EndDate
            // 3. CampaignStage must be IsActive
            // 4. CampaignStage StartDate <= date <= EndDate
            // 5. CampaignProduct must be IsActive
            // 6. CampaignProductStagePrice must exist

            var activeCampaignPrice = await _db.CampaignProductStagePrices
                .AsNoTracking()
                .Where(sp => 
                    sp.CampaignStage!.IsActive && 
                    sp.CampaignStage.StartDate <= date && 
                    sp.CampaignStage.EndDate >= date &&
                    sp.CampaignProduct!.IsActive &&
                    sp.CampaignProduct.ProductId == productId &&
                    sp.CampaignProduct.Campaign!.IsActive &&
                    sp.CampaignProduct.Campaign.StartDate <= date &&
                    sp.CampaignProduct.Campaign.EndDate >= date
                )
                .Select(sp => new
                {
                    sp.Price,
                    sp.CampaignStageId,
                    CampaignStageName = sp.CampaignStage!.Name,
                    StageEndDate = sp.CampaignStage.EndDate,
                    CampaignEndDate = sp.CampaignProduct!.Campaign!.EndDate,
                    CampaignId = sp.CampaignProduct!.CampaignId,
                    CampaignName = sp.CampaignProduct!.Campaign!.Name
                })
                .FirstOrDefaultAsync();

            if (activeCampaignPrice != null)
            {
                return new ProductPriceResolutionResult
                {
                    ProductBasePrice = product.Price,
                    EffectivePrice = activeCampaignPrice.Price,
                    IsCampaignPrice = true,
                    CampaignId = activeCampaignPrice.CampaignId,
                    CampaignName = activeCampaignPrice.CampaignName,
                    CampaignStageId = activeCampaignPrice.CampaignStageId,
                    CampaignStageName = activeCampaignPrice.CampaignStageName,
                    CampaignStageEndDate = activeCampaignPrice.StageEndDate < activeCampaignPrice.CampaignEndDate
                        ? activeCampaignPrice.StageEndDate
                        : activeCampaignPrice.CampaignEndDate
                };
            }

            return defaultResult;
        }

        public async Task<IReadOnlyDictionary<Guid, ProductPriceResolutionResult>> ResolvePricesAsync(IReadOnlyCollection<Guid> productIds, DateTimeOffset? currentDate = null)
        {
            if (productIds.Count == 0)
                return new Dictionary<Guid, ProductPriceResolutionResult>();

            var date = currentDate ?? DateTimeOffset.UtcNow;

            var basePrices = await _db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Price })
                .ToDictionaryAsync(p => p.Id, p => p.Price);

            // Same rules as ResolvePriceAsync, for all products at once
            var campaignPrices = await _db.CampaignProductStagePrices
                .AsNoTracking()
                .Where(sp =>
                    sp.CampaignStage!.IsActive &&
                    sp.CampaignStage.StartDate <= date &&
                    sp.CampaignStage.EndDate >= date &&
                    sp.CampaignProduct!.IsActive &&
                    productIds.Contains(sp.CampaignProduct.ProductId) &&
                    sp.CampaignProduct.Campaign!.IsActive &&
                    sp.CampaignProduct.Campaign.StartDate <= date &&
                    sp.CampaignProduct.Campaign.EndDate >= date
                )
                .Select(sp => new
                {
                    sp.CampaignProduct!.ProductId,
                    sp.Price,
                    sp.CampaignStageId,
                    CampaignStageName = sp.CampaignStage!.Name,
                    StageEndDate = sp.CampaignStage.EndDate,
                    CampaignEndDate = sp.CampaignProduct!.Campaign!.EndDate,
                    CampaignId = sp.CampaignProduct!.CampaignId,
                    CampaignName = sp.CampaignProduct!.Campaign!.Name
                })
                .ToListAsync();

            var campaignByProduct = campaignPrices
                .GroupBy(cp => cp.ProductId)
                .ToDictionary(g => g.Key, g => g.First());

            var results = new Dictionary<Guid, ProductPriceResolutionResult>(basePrices.Count);
            foreach (var (productId, basePrice) in basePrices)
            {
                results[productId] = campaignByProduct.TryGetValue(productId, out var campaign)
                    ? new ProductPriceResolutionResult
                    {
                        ProductBasePrice = basePrice,
                        EffectivePrice = campaign.Price,
                        IsCampaignPrice = true,
                        CampaignId = campaign.CampaignId,
                        CampaignName = campaign.CampaignName,
                        CampaignStageId = campaign.CampaignStageId,
                        CampaignStageName = campaign.CampaignStageName,
                        CampaignStageEndDate = campaign.StageEndDate < campaign.CampaignEndDate
                            ? campaign.StageEndDate
                            : campaign.CampaignEndDate
                    }
                    : new ProductPriceResolutionResult
                    {
                        ProductBasePrice = basePrice,
                        EffectivePrice = basePrice,
                        IsCampaignPrice = false
                    };
            }

            return results;
        }
    }
}
