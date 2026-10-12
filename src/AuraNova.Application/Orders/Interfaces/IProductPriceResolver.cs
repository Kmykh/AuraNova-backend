using System;
using System.Threading.Tasks;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IProductPriceResolver
    {
        Task<ProductPriceResolutionResult> ResolvePriceAsync(Guid productId, DateTimeOffset? currentDate = null);

        /// <summary>
        /// Resuelve los precios de varios productos con 2 consultas en total (en lugar de 2 por producto).
        /// Los productos inexistentes no aparecen en el resultado.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, ProductPriceResolutionResult>> ResolvePricesAsync(IReadOnlyCollection<Guid> productIds, DateTimeOffset? currentDate = null);
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
