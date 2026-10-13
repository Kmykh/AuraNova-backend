using System.Collections.Generic;

namespace AuraNova.Application.BusinessSettings.DTOs
{
    public class ColorOptionDto
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>"#rrggbb", o "multicolor" para el surtido.</summary>
        public string Hex { get; set; } = string.Empty;
    }

    /// <summary>Paleta de colores y tipos de flor que se ofrecen al personalizar productos.</summary>
    public class CustomizationOptionsDto
    {
        public List<ColorOptionDto> Colors { get; set; } = new List<ColorOptionDto>();
        public List<string> FlowerTypes { get; set; } = new List<string>();
    }
}
