using AuraNova.Application.Storage.Interfaces;

namespace AuraNova.Infrastructure.Storage
{
    public class ReferenceImageService(IFileStorageService storage) : IReferenceImageService
    {
        public const string Folder = "custom-orders";
        /// <summary>Fondos de tarjeta: dentro de la carpeta privada de pedidos personalizados.</summary>
        public const string CardFolder = "custom-orders/cards";
        private const int MaxDecodedBytes = 8 * 1024 * 1024;
        private const int MaxCardDecodedBytes = 2 * 1024 * 1024;

        public async Task<string?> PersistAsync(string? reference, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            var (bytes, mimeType) = Decode(reference, MaxDecodedBytes, "La imagen de referencia");
            using var stream = new MemoryStream(bytes);
            return await storage.UploadAsync(stream, "reference.jpg", mimeType, Folder);
        }

        public async Task<string> PersistCardBackgroundAsync(string dataUrl, CancellationToken cancellationToken = default)
        {
            var (bytes, mimeType) = Decode(dataUrl, MaxCardDecodedBytes, "La foto de la tarjeta");
            using var stream = new MemoryStream(bytes);
            return await storage.UploadAsync(stream, "card.jpg", mimeType, CardFolder);
        }

        private static (byte[] Bytes, string MimeType) Decode(string reference, int maxBytes, string label)
        {
            reference = reference.Trim();

            // Solo se acepta la imagen en sí. Aceptar rutas o URLs permitiría enlazar archivos de otros
            // pedidos (que viven en el bucket privado) o URLs externas usadas como pixel de rastreo.
            if (!reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidImageException($"{label} no es válida.");

            var comma = reference.IndexOf(',');
            if (comma < 0)
                throw new InvalidImageException($"{label} no es válida.");

            var header = reference[5..comma];
            if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidImageException($"{label} no es válida.");

            var mimeType = header[..^";base64".Length].ToLowerInvariant();
            if (!mimeType.StartsWith("image/"))
                throw new InvalidImageException($"{label} debe ser una imagen.");

            var base64 = reference[(comma + 1)..];
            if (base64.Length / 4L * 3 > maxBytes)
                throw new InvalidImageException($"{label} no debe exceder los {maxBytes / (1024 * 1024)} MB.");

            try
            {
                return (Convert.FromBase64String(base64), mimeType);
            }
            catch (FormatException)
            {
                throw new InvalidImageException($"{label} no es válida.");
            }
        }

        public async Task<string> PersistFileAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidImageException("Solo se aceptan imágenes (JPG, PNG o WEBP).");

            // El decorador de storage valida el contenido real y lo convierte a WebP sin metadatos
            return await storage.UploadAsync(content, "reference.jpg", contentType.ToLowerInvariant(), Folder);
        }
    }
}
