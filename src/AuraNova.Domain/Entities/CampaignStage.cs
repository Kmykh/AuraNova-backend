using System;
using System.Collections.Generic;

namespace AuraNova.Domain.Entities
{
    public class CampaignStage
    {
        public Guid Id { get; set; }
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        public string Name { get; set; } = null!;
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<CampaignProductStagePrice>? StagePrices { get; set; }

        public CampaignStage()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
