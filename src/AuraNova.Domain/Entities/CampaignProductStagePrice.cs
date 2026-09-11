using System;

namespace AuraNova.Domain.Entities
{
    public class CampaignProductStagePrice
    {
        public Guid Id { get; set; }
        
        public Guid CampaignProductId { get; set; }
        public CampaignProduct? CampaignProduct { get; set; }

        public Guid CampaignStageId { get; set; }
        public CampaignStage? CampaignStage { get; set; }

        public decimal Price { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public CampaignProductStagePrice()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
