using System;

namespace AuraNova.Domain.Entities
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }

        // Customer Selection
        public string? SelectedPrimaryColor { get; set; }
        public string? SelectedSecondaryColor { get; set; }
        public string? SelectedFlowerType { get; set; }
        public string? SelectedFlowerColor { get; set; }
        public bool HasLights { get; set; } = false;
        public bool HasButterfly { get; set; } = false;
        public bool HasPhraseCard { get; set; } = false;
        public string? PhraseText { get; set; }
        public string? PhraseFont { get; set; }

        // Campaign Tracking
        public Guid? CampaignId { get; set; }
        public Guid? CampaignStageId { get; set; }
        public string? AppliedCampaignName { get; set; }
        public string? AppliedCampaignStageName { get; set; }

        public OrderItem()
        {
            Id = Guid.NewGuid();
        }
    }
}