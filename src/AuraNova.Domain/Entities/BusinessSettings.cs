using System;
using System.Collections.Generic;

namespace AuraNova.Domain.Entities
{
    public class BusinessSettings
    {
        public Guid Id { get; set; }
        
        public string BusinessName { get; set; } = string.Empty;
        public string WhatsAppNumber { get; set; } = string.Empty;
        
        public string YapeHolderName { get; set; } = string.Empty;
        public string? YapeQrImageUrl { get; set; }
        public string? TikTokUsername { get; set; }

        // Opciones de personalización que el panel ofrece al crear productos
        public List<ColorOption> ColorPalette { get; set; } = new List<ColorOption>();
        public List<string> FlowerTypeOptions { get; set; } = new List<string>();
        
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public BusinessSettings()
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
