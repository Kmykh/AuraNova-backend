using System;
using System.Collections.Generic;

namespace AuraNova.Domain.Entities
{
    public class Campaign
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<CampaignStage>? Stages { get; set; }
        public ICollection<CampaignProduct>? Products { get; set; }

        public Campaign()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
