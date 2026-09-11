using System;
using System.Threading.Tasks;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IProductPriceResolver
    {
        Task<ProductPriceResolutionResult> ResolvePriceAsync(Guid productId, DateTimeOffset? currentDate = null);
    }

    public class ProductPriceResolutionResult
    {
        public decimal ProductBasePrice { get; set; }
        public decimal EffectivePrice { get; set; }
        
        public bool IsCampaignPrice { get; set; }
        
        public Guid? CampaignId { get; set; }
        public string? CampaignName { get; set; }
        
        public Guid? CampaignStageId { get; set; }
        public string? CampaignStageName { get; set; }
    }
}
