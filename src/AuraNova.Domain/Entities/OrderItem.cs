using System;
using System.Collections.Generic;

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

        // Customer Selection: una opción por elemento de la lista (nunca texto separado por comas)
        public List<string> SelectedColors { get; set; } = new List<string>();
        public List<string> SelectedFlowerTypes { get; set; } = new List<string>();

        // Campos anteriores: se siguen llenando (primer color / primera flor) para no romper reportes viejos
        public string? SelectedPrimaryColor { get; set; }
        public string? SelectedSecondaryColor { get; set; }
        public string? SelectedFlowerType { get; set; }
        public string? SelectedFlowerColor { get; set; }
        public bool HasLights { get; set; } = false;
        public bool HasButterfly { get; set; } = false;
        public bool HasPhraseCard { get; set; } = false;
        /// <summary>Accesorios elegidos con su precio de ese momento (ya sumados en UnitPrice).</summary>
        public List<OrderItemAccessory> Accessories { get; set; } = new List<OrderItemAccessory>();
        public string? PhraseText { get; set; }
        public string? PhraseFont { get; set; }
        /// <summary>Tarjeta: para quién es (se imprime "Para: …").</summary>
        public string? PhraseTo { get; set; }
        /// <summary>Tarjeta: quién la envía (se imprime la firma).</summary>
        public string? PhraseFrom { get; set; }
        /// <summary>Diseño de la tarjeta: marfil, rosa, pergamino, noir, salvia o foto.</summary>
        public string? PhraseTheme { get; set; }
        /// <summary>Foto que subió el cliente como fondo de la tarjeta (bucket privado, "private/…").</summary>
        public string? CardBackgroundPath { get; set; }

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