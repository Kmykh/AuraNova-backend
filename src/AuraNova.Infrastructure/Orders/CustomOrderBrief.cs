using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>
    /// Arma la parte "brief" (imágenes de referencia, enlaces, fecha deseada…) de las respuestas de pedidos
    /// personalizados, firmando todas las imágenes privadas en una sola llamada al storage.
    /// </summary>
    internal static class CustomOrderBrief
    {
        public static IEnumerable<string> StoredPaths(Order order)
        {
            var paths = (order.ReferenceImages ?? []).OrderBy(i => i.SortOrder).Select(i => i.Path).ToList();
            // Pedidos anteriores a la galería solo tienen ReferenceImageUrl
            if (paths.Count == 0 && !string.IsNullOrWhiteSpace(order.ReferenceImageUrl))
                paths.Add(order.ReferenceImageUrl);
            return paths;
        }

        public static List<string> ImageUrls(Order order, IReadOnlyDictionary<string, string> signedUrls) =>
            StoredPaths(order).Select(p => signedUrls.GetValueOrDefault(p)).OfType<string>().ToList();

        /// <summary>Valida y limpia los enlaces de inspiración: solo http(s), sin repetir, máximo 5.</summary>
        public static List<string> CleanLinks(IEnumerable<string>? links)
        {
            var result = new List<string>();
            foreach (var raw in links ?? [])
            {
                var link = raw?.Trim();
                if (string.IsNullOrEmpty(link))
                    continue;

                if (link.Length > 500 ||
                    !Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                    throw new OrderValidationException($"El enlace de inspiración no es válido: {link}");

                if (!result.Contains(uri.AbsoluteUri))
                    result.Add(uri.AbsoluteUri);
            }

            if (result.Count > 5)
                throw new OrderValidationException("Puedes compartir hasta 5 enlaces de inspiración.");

            return result;
        }
    }
}
