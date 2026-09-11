using System;
using System.Collections.Generic;

namespace AuraNova.Domain.Entities
{
    public class CampaignProduct
    {
        public Guid Id { get; set; }
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }

        public ICollection<CampaignProductStagePrice>? StagePrices { get; set; }

        public CampaignProduct()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
