using System.Globalization;
using System.Text;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>Accesorios del negocio: los iniciales, las claves y lo que ofrece cada producto.</summary>
    public static class AccessoryCatalog
    {
        public const string Lights = "luces";
        public const string Butterfly = "mariposas";
        public const int MaxAccessories = 30;
        public const decimal MaxPrice = 1000m;

        /// <summary>Mientras el negocio no configure su lista: luces y mariposas, sin costo (como antes).</summary>
        public static List<AccessoryOption> Defaults() =>
        [
            new() { Key = Lights, Name = "Luces LED", Price = 0m, Active = true },
            new() { Key = Butterfly, Name = "Mariposas", Price = 0m, Active = true }
        ];

        public static List<AccessoryOption> Effective(List<AccessoryOption>? configured) =>
            configured is { Count: > 0 } ? configured : Defaults();

        /// <summary>"Luces LED" → "luces-led": sin tildes, minúsculas y guiones.</summary>
        public static string Slug(string value)
        {
            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                builder.Append(char.IsLetterOrDigit(c) ? c : '-');
            }
            var slug = string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
            return slug.Length <= 30 ? slug : slug[..30].TrimEnd('-');
        }

        /// <summary>Accesorios de una línea del pedido; los pedidos anteriores solo tenían luces/mariposas como sí/no.</summary>
        public static List<OrderItemAccessoryDto> ForItem(OrderItem item)
        {
            if (item.Accessories is { Count: > 0 })
                return [.. item.Accessories.Select(a => new OrderItemAccessoryDto { Key = a.Key, Name = a.Name, Price = a.Price })];
            var legacy = new List<OrderItemAccessoryDto>();
            if (item.HasLights) legacy.Add(new() { Key = Lights, Name = "Luces LED" });
            if (item.HasButterfly) legacy.Add(new() { Key = Butterfly, Name = "Mariposas" });
            return legacy;
        }

        /// <summary>Lo que ofrece el producto; los productos anteriores solo tenían "luces" y "mariposas" como sí/no.</summary>
        public static List<string> ProductKeys(Product product)
        {
            if (product.AvailableAccessories is { Count: > 0 })
                return product.AvailableAccessories;
            var legacy = new List<string>();
            if (product.AllowsLights) legacy.Add(Lights);
            if (product.AllowsButterfly) legacy.Add(Butterfly);
            return legacy;
        }
    }
}
