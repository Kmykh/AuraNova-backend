using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AuraNova.Application.Orders.DTOs
{
    public class CreateOrderItemRequest
    {
        [Required] public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Quantity { get; set; }

        /// <summary>Colores elegidos, uno por elemento.</summary>
        public List<string>? SelectedColors { get; set; }
        /// <summary>Tipos de flor elegidos, uno por elemento.</summary>
        public List<string>? SelectedFlowerTypes { get; set; }

        // Formato anterior (texto). Se acepta mientras haya clientes con la versión vieja de la web.
        public string? SelectedPrimaryColor { get; set; }
        public string? SelectedSecondaryColor { get; set; }
        public string? SelectedFlowerType { get; set; }
        public string? SelectedFlowerColor { get; set; }
        public bool HasLights { get; set; }
        public bool HasButterfly { get; set; }

        /// <summary>Claves de los accesorios elegidos. Null = se toman de HasLights/HasButterfly (carritos anteriores).</summary>
        public List<string>? SelectedAccessories { get; set; }
        public bool HasPhraseCard { get; set; }
        public string? PhraseText { get; set; }
        public string? PhraseFont { get; set; }

        [MaxLength(60)]
        public string? PhraseTo { get; set; }

        [MaxLength(60)]
        public string? PhraseFrom { get; set; }

        /// <summary>marfil, rosa, pergamino, noir, salvia o foto.</summary>
        [MaxLength(20)]
        public string? PhraseTheme { get; set; }

        /// <summary>
        /// Foto propia para el fondo de la tarjeta, como data URL (la web la comprime antes de enviarla).
        /// El servidor la valida, la re-codifica sin metadatos y la guarda en el bucket privado.
        /// </summary>
        [MaxLength(3_000_000)]
        public string? CardBackgroundImage { get; set; }

        /// <summary>Ruta ya guardada de esa foto. La asigna el servidor; nunca viene del cliente.</summary>
        [JsonIgnore]
        public string? CardBackgroundPath { get; set; }
    }
}
