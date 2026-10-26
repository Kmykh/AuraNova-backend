using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>
    /// Opciones que el cliente eligió para un producto, ya validadas contra lo que el producto ofrece.
    /// Cada color o flor es un elemento de la lista: nada de texto separado por comas.
    /// </summary>
    public sealed record OrderItemOptions(
        List<string> Colors,
        List<string> FlowerTypes,
        bool HasLights,
        bool HasButterfly,
        bool HasPhraseCard,
        string? PhraseText,
        string? PhraseFont,
        string? PhraseTo = null,
        string? PhraseFrom = null,
        string? PhraseTheme = null,
        string? CardBackgroundPath = null)
    {
        /// <summary>Diseños de tarjeta que ofrece la web ("foto" = fondo subido por el cliente).</summary>
        public static readonly string[] CardThemes = ["marfil", "rosa", "pergamino", "noir", "salvia", "foto"];

        /// <summary>El cliente deja que el taller combine los colores.</summary>
        public const string MixedColors = "Surtido multicolor";

        public const int MaxSelections = 6;
        private const int MaxOptionLength = 50;
        private const int MaxPhraseLength = 1000;

        // Valores que enviaba la versión anterior de la web para el surtido
        private static readonly string[] LegacyMixedLabels = ["Surtido (Varios colores)", "Surtido", "Varios colores"];

        public static OrderItemOptions Resolve(CreateOrderItemRequest item, Product product)
        {
            var colors = Pick(
                item.SelectedColors,
                [item.SelectedPrimaryColor, item.SelectedSecondaryColor]);
            var flowers = Pick(
                item.SelectedFlowerTypes,
                [item.SelectedFlowerType, item.SelectedFlowerColor]);

            colors = colors
                .Select(c => LegacyMixedLabels.Contains(c, StringComparer.OrdinalIgnoreCase) ? MixedColors : c)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // El surtido reemplaza cualquier otro color
            if (colors.Contains(MixedColors, StringComparer.OrdinalIgnoreCase))
                colors = [MixedColors];

            var resolvedColors = MatchAvailable(colors, product.AvailableColors, product.Name, "color", allowMixed: true);
            var resolvedFlowers = MatchAvailable(flowers, product.AvailableFlowerTypes, product.Name, "tipo de flor", allowMixed: false);

            var hasPhraseCard = item.HasPhraseCard && product.AllowsPhraseCard;
            var phrase = hasPhraseCard ? item.PhraseText?.Trim() : null;
            if (phrase is { Length: > MaxPhraseLength })
                throw new OrderValidationException($"La dedicatoria de '{product.Name}' supera los {MaxPhraseLength} caracteres.");

            var font = hasPhraseCard ? item.PhraseFont?.Trim() : null;
            if (font is { Length: > MaxOptionLength })
                font = font[..MaxOptionLength];

            var theme = hasPhraseCard ? item.PhraseTheme?.Trim().ToLowerInvariant() : null;
            if (theme != null && !CardThemes.Contains(theme))
                theme = null;
            var background = hasPhraseCard ? item.CardBackgroundPath : null;
            if (theme == "foto" && background == null)
                theme = null;

            return new OrderItemOptions(
                resolvedColors,
                resolvedFlowers,
                item.HasLights && product.AllowsLights,
                item.HasButterfly && product.AllowsButterfly,
                hasPhraseCard,
                string.IsNullOrEmpty(phrase) ? null : phrase,
                string.IsNullOrEmpty(font) ? null : font,
                hasPhraseCard ? Clean(item.PhraseTo, 60) : null,
                hasPhraseCard ? Clean(item.PhraseFrom, 60) : null,
                theme,
                background);
        }

        private static string? Clean(string? value, int max)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;
            return trimmed.Length <= max ? trimmed : trimmed[..max];
        }

        /// <summary>Clave para reconocer el mismo producto con la misma personalización.</summary>
        public string Key => string.Join("|",
            string.Join(",", Colors), string.Join(",", FlowerTypes), HasLights, HasButterfly, HasPhraseCard, PhraseText, PhraseFont,
            PhraseTo, PhraseFrom, PhraseTheme, CardBackgroundPath);

        /// <summary>Lista nueva si viene; si no, los campos de texto anteriores (que podían traer comas).</summary>
        private static List<string> Pick(List<string>? list, string?[] legacy)
        {
            var source = list is { Count: > 0 }
                ? list
                : legacy.Where(v => !string.IsNullOrWhiteSpace(v)).SelectMany(v => v!.Split(','));

            var values = source
                .Select(v => v?.Trim() ?? string.Empty)
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (values.Count > MaxSelections)
                throw new OrderValidationException($"Puedes elegir hasta {MaxSelections} opciones por producto.");

            return values;
        }

        private static List<string> MatchAvailable(List<string> chosen, List<string>? available, string productName, string label, bool allowMixed)
        {
            // Producto sin esa personalización: no se guarda nada aunque el cliente lo envíe
            if (available is not { Count: > 0 })
                return [];

            var result = new List<string>(chosen.Count);
            foreach (var value in chosen)
            {
                if (allowMixed && string.Equals(value, MixedColors, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(MixedColors);
                    continue;
                }

                var match = available.FirstOrDefault(a => string.Equals(a.Trim(), value, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new OrderValidationException(
                        $"El {label} '{value}' ya no está disponible para '{productName}'. Actualiza tu carrito e inténtalo de nuevo.");

                result.Add(match.Trim().Length > MaxOptionLength ? match.Trim()[..MaxOptionLength] : match.Trim());
            }

            return result;
        }
    }
}
