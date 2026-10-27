using System.Text.RegularExpressions;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>
    /// Opciones que el cliente eligió para un producto, ya validadas contra lo que el producto ofrece.
    /// Cada color o flor es un elemento de la lista: nada de texto separado por comas.
    /// </summary>
    public sealed partial record OrderItemOptions(
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
        string? CardBackgroundPath = null,
        List<OrderItemAccessory>? Accessories = null)
    {
        /// <summary>Accesorios elegidos con el precio vigente del negocio.</summary>
        public List<OrderItemAccessory> AccessoryList => Accessories ?? [];

        /// <summary>Lo que suman los accesorios a cada unidad.</summary>
        public decimal AccessoriesPrice => AccessoryList.Sum(a => a.Price);

        /// <summary>Diseños de tarjeta que ofrece la web ("foto" = fondo subido por el cliente).</summary>
        public static readonly string[] CardThemes = ["marfil", "rosa", "pergamino", "noir", "salvia", "foto"];

        /// <summary>El cliente deja que el taller combine los colores.</summary>
        public const string MixedColors = "Surtido multicolor";

        /// <summary>
        /// Color que el cliente pide aunque no esté en la lista del producto: "A pedido: Verde menta (#98FB98)".
        /// El taller ve el nombre y el tono exacto, y confirma con el cliente si no tiene ese material.
        /// </summary>
        public const string CustomColorPrefix = "A pedido: ";

        [GeneratedRegex(@"^A pedido:\s*(?<name>[\p{L}\p{N} .'-]{1,30}?)\s*\((?<hex>#[0-9A-Fa-f]{6})\)$")]
        private static partial Regex CustomColorPattern();

        /// <summary>Color a pedido con el formato normalizado, o null si no lo es.</summary>
        public static string? NormalizeCustomColor(string value)
        {
            var match = CustomColorPattern().Match(value.Trim());
            if (!match.Success) return null;
            var name = match.Groups["name"].Value.Trim();
            return name.Length == 0 ? null : $"{CustomColorPrefix}{name} ({match.Groups["hex"].Value.ToUpperInvariant()})";
        }

        public const int MaxSelections = 6;
        private const int MaxOptionLength = 50;
        private const int MaxPhraseLength = 1000;

        // Valores que enviaba la versión anterior de la web para el surtido
        private static readonly string[] LegacyMixedLabels = ["Surtido (Varios colores)", "Surtido", "Varios colores"];

        public static OrderItemOptions Resolve(CreateOrderItemRequest item, Product product, List<AccessoryOption>? catalog = null)
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

            // Flor por unidad (o con pocas flores): no se eligen más colores ni tipos de flor que los que lleva
            if (product.MaxChoices is int limit)
            {
                var tooMany = resolvedColors.Count > limit || resolvedFlowers.Count > limit ||
                              (limit == 1 && resolvedColors.Contains(MixedColors));
                if (tooMany)
                    throw new OrderValidationException(limit == 1
                        ? $"'{product.Name}' es una sola flor: elige un solo color. Quítala del carrito y vuelve a agregarla."
                        : $"Para '{product.Name}' puedes elegir hasta {limit} colores y {limit} tipos de flor.");
            }

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

            var accessories = ResolveAccessories(item, product, AccessoryCatalog.Effective(catalog));

            return new OrderItemOptions(
                resolvedColors,
                resolvedFlowers,
                accessories.Any(a => a.Key == AccessoryCatalog.Lights),
                accessories.Any(a => a.Key == AccessoryCatalog.Butterfly),
                hasPhraseCard,
                string.IsNullOrEmpty(phrase) ? null : phrase,
                string.IsNullOrEmpty(font) ? null : font,
                hasPhraseCard ? Clean(item.PhraseTo, 60) : null,
                hasPhraseCard ? Clean(item.PhraseFrom, 60) : null,
                theme,
                background,
                accessories);
        }

        /// <summary>
        /// Accesorios que ofrece el producto, con el nombre y precio de la configuración del negocio (nunca el que manda la web).
        /// La web anterior solo enviaba luces/mariposas como sí/no: lo que ya no se ofrece se ignora, como antes.
        /// </summary>
        private static List<OrderItemAccessory> ResolveAccessories(CreateOrderItemRequest item, Product product, List<AccessoryOption> catalog)
        {
            var legacy = item.SelectedAccessories == null;
            var requested = legacy
                ? new[] { item.HasLights ? AccessoryCatalog.Lights : null, item.HasButterfly ? AccessoryCatalog.Butterfly : null }
                    .OfType<string>().ToList()
                : item.SelectedAccessories!.Select(AccessoryCatalog.Slug).Where(k => k.Length > 0).Distinct().ToList();

            if (requested.Count > AccessoryCatalog.MaxAccessories)
                throw new OrderValidationException($"Puedes elegir hasta {AccessoryCatalog.MaxAccessories} accesorios por producto.");

            var offered = AccessoryCatalog.ProductKeys(product);
            var result = new List<OrderItemAccessory>(requested.Count);
            foreach (var key in requested)
            {
                var option = catalog.FirstOrDefault(a => a.Key == key);
                if (option is not { Active: true } || !offered.Contains(key))
                {
                    if (legacy) continue;
                    throw new OrderValidationException(
                        $"El accesorio '{option?.Name ?? key}' ya no está disponible para '{product.Name}'. Actualiza tu carrito e inténtalo de nuevo.");
                }
                result.Add(new OrderItemAccessory { Key = option.Key, Name = option.Name, Price = option.Price });
            }
            return result;
        }

        private static string? Clean(string? value, int max)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;
            return trimmed.Length <= max ? trimmed : trimmed[..max];
        }

        /// <summary>Clave para reconocer el mismo producto con la misma personalización.</summary>
        public string Key => string.Join("|",
            string.Join(",", Colors), string.Join(",", FlowerTypes), string.Join(",", AccessoryList.Select(a => a.Key).Order()),
            HasPhraseCard, PhraseText, PhraseFont, PhraseTo, PhraseFrom, PhraseTheme, CardBackgroundPath);

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

                // Color que no está en la lista: se acepta con nombre y tono (solo en colores, no en flores)
                if (allowMixed && NormalizeCustomColor(value) is { } custom)
                {
                    result.Add(custom);
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
