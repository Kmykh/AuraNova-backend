using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;

namespace AuraNova.Infrastructure.Payments
{
    /// <summary>
    /// Datos del pedido que se envían a Mercado Pago: productos, comprador y dirección. Le sirven a su motor
    /// antifraude para aprobar más pagos y cuentan en la medición de calidad de la integración.
    /// </summary>
    public static class MercadoPagoOrderData
    {
        /// <summary>Categoría de Mercado Pago para ramos, cajas y regalos (Hogar).</summary>
        public const string ProductCategory = "home";
        public const string ServiceCategory = "services";

        /// <summary>
        /// Líneas del pedido. Mercado Pago exige que sumen exactamente el monto cobrado: en el adelanto o el saldo
        /// los precios se prorratean y, si no cuadran al céntimo, se envía una sola línea con el resumen.
        /// </summary>
        public static IReadOnlyList<GatewayItem> Items(Order order, decimal amount, Func<string?, string?> pictureUrl)
        {
            var lines = OrderLines(order, pictureUrl);
            var fitted = lines.Count > 0 && lines.Sum(l => l.Quantity * l.UnitPrice) == order.Total
                ? Prorate(lines, amount)
                : null;
            return fitted ?? [Summary(order, amount, lines)];
        }

        public static (string? FirstName, string? LastName) SplitName(string? fullName)
        {
            var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => (null, null),
                1 => (Truncate(parts[0], 60), null),
                _ => (Truncate(parts[0], 60), Truncate(string.Join(' ', parts[1..]), 60))
            };
        }

        /// <summary>Dónde se entrega: la dirección del delivery o la agencia en provincia. El punto de encuentro no tiene.</summary>
        public static GatewayAddress? ShipmentAddress(Order order) =>
            order.DeliveryType == DeliveryType.MeetingPoint || string.IsNullOrWhiteSpace(order.DeliveryAddress)
                ? null
                : new GatewayAddress(
                    Truncate(order.DeliveryAddress.Trim(), 120)!,
                    Truncate(order.District ?? order.Province, 60),
                    Truncate(order.Department, 60),
                    Truncate(order.DeliveryReference, 120));

        /// <summary>Dirección del comprador: solo cuando el delivery es para él (no un regalo ni una agencia).</summary>
        public static GatewayAddress? PayerAddress(Order order) =>
            order.DeliveryType == DeliveryType.Delivery && string.IsNullOrWhiteSpace(order.RecipientName)
                ? ShipmentAddress(order)
                : null;

        /// <summary>DNI del comprador si lo dio al pedir (envío a provincia que recoge él mismo).</summary>
        public static string? PayerDocument(Order order) =>
            string.IsNullOrWhiteSpace(order.RecipientName) && !string.IsNullOrWhiteSpace(order.RecipientDocument)
                ? order.RecipientDocument
                : null;

        /// <summary>Celular peruano de 9 dígitos (sin +51 ni espacios), o null si no es válido.</summary>
        public static string? Phone(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 11 && digits.StartsWith("51")) digits = digits[2..];
            return digits.Length == 9 ? digits : null;
        }

        private static List<GatewayItem> OrderLines(Order order, Func<string?, string?> pictureUrl)
        {
            var lines = (order.Items ?? [])
                .Select(i => new GatewayItem(
                    // Mercado Pago acepta códigos de hasta 30 caracteres
                    i.ProductId.ToString("N")[..24],
                    Truncate(i.Product?.Name, 120) ?? "Detalle Aura Nova",
                    Truncate(Details(i), 250),
                    ProductCategory,
                    i.Quantity,
                    i.UnitPrice,
                    PublicPicture(pictureUrl(i.Product?.ImageUrl))))
                .ToList();

            if (order.CustomizationCost > 0)
                lines.Add(new GatewayItem(
                    $"{order.OrderCode}-PERSONALIZADO",
                    order.IsCustomOrder ? "Detalle personalizado a medida" : "Personalización",
                    Truncate(order.CustomizationNotes, 250),
                    ProductCategory, 1, order.CustomizationCost, null));

            if (order.DeliveryCost is > 0)
                lines.Add(new GatewayItem(
                    $"{order.OrderCode}-ENTREGA",
                    order.DeliveryType == DeliveryType.MeetingPoint ? "Entrega en punto de encuentro" : "Delivery",
                    null, ServiceCategory, 1, order.DeliveryCost.Value, null));

            return lines;
        }

        /// <summary>Ajusta los precios para que sumen el monto cobrado (adelanto o saldo); null si no cuadra al céntimo.</summary>
        private static List<GatewayItem>? Prorate(List<GatewayItem> lines, decimal amount)
        {
            var total = lines.Sum(l => l.Quantity * l.UnitPrice);
            if (total == amount)
                return lines;

            var factor = amount / total;
            var scaled = lines
                .Select(l => l with { UnitPrice = Math.Round(l.UnitPrice * factor, 2, MidpointRounding.AwayFromZero) })
                .ToList();

            // El redondeo puede dejar céntimos sueltos: los absorbe una línea de una sola unidad
            var difference = amount - scaled.Sum(l => l.Quantity * l.UnitPrice);
            if (difference != 0)
            {
                var index = scaled.FindIndex(l => l.Quantity == 1 && l.UnitPrice + difference > 0);
                if (index < 0)
                    return null;
                scaled[index] = scaled[index] with { UnitPrice = scaled[index].UnitPrice + difference };
            }

            return scaled.All(l => l.UnitPrice > 0) ? scaled : null;
        }

        private static GatewayItem Summary(Order order, decimal amount, List<GatewayItem> lines) => new(
            order.OrderCode,
            $"Pedido {order.OrderCode} - Aura Nova",
            Truncate(lines.Count > 0
                ? string.Join(", ", lines.Select(l => $"{l.Quantity}x {l.Title}"))
                : order.IsCustomOrder ? "Detalle personalizado a medida" : null, 250),
            ProductCategory, 1, amount,
            lines.Select(l => l.PictureUrl).FirstOrDefault(p => p != null));

        private static string? Details(OrderItem item)
        {
            var parts = new List<string>();
            if (item.SelectedColors is { Count: > 0 }) parts.Add($"Colores: {string.Join(", ", item.SelectedColors)}");
            if (item.SelectedFlowerTypes is { Count: > 0 }) parts.Add($"Flores: {string.Join(", ", item.SelectedFlowerTypes)}");
            var accessories = Orders.AccessoryCatalog.ForItem(item);
            if (accessories.Count > 0) parts.Add($"Con {string.Join(", ", accessories.Select(a => a.Name.ToLowerInvariant()))}");
            if (item.HasPhraseCard) parts.Add("con tarjeta de frase");
            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }

        /// <summary>Solo URLs públicas https (no imágenes en base64 ni rutas internas).</summary>
        private static string? PublicPicture(string? url) =>
            url != null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && url.Length <= 500 ? url : null;

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length <= max ? value : value[..max];
        }
    }
}
