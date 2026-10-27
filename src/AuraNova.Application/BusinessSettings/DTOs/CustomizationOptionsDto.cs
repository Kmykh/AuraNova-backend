using System.Collections.Generic;

namespace AuraNova.Application.BusinessSettings.DTOs
{
    public class ColorOptionDto
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>"#rrggbb", o "multicolor" para el surtido.</summary>
        public string Hex { get; set; } = string.Empty;
    }

    /// <summary>Accesorio con precio extra (luces, mariposas, perlas…).</summary>
    public class AccessoryOptionDto
    {
        /// <summary>Clave fija (se genera del nombre la primera vez); es lo que guardan los productos.</summary>
        public string? Key { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool Active { get; set; } = true;
    }

    /// <summary>Paleta de colores, tipos de flor y accesorios que se ofrecen al personalizar productos.</summary>
    public class CustomizationOptionsDto
    {
        public List<ColorOptionDto> Colors { get; set; } = new List<ColorOptionDto>();
        public List<string> FlowerTypes { get; set; } = new List<string>();
        /// <summary>Al guardar: null = no se cambian (el editor anterior solo enviaba colores y flores).</summary>
        public List<AccessoryOptionDto>? Accessories { get; set; }
    }
}
