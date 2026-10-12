using AuraNova.Application.Storage.Interfaces;

namespace AuraNova.Infrastructure.Storage
{
    public class ReferenceImageService(IFileStorageService storage) : IReferenceImageService
    {
        public const string Folder = "custom-orders";
        private const int MaxDecodedBytes = 8 * 1024 * 1024;

        public async Task<string?> PersistAsync(string? reference, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            reference = reference.Trim();

            // Solo se acepta la imagen en sí. Aceptar rutas o URLs permitiría enlazar archivos de otros
            // pedidos (que viven en el bucket privado) o URLs externas usadas como pixel de rastreo.
            if (!reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidImageException("La imagen de referencia no es válida.");

            var comma = reference.IndexOf(',');
            if (comma < 0)
                throw new InvalidImageException("La imagen de referencia no es válida.");

            var header = reference[5..comma];
            if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidImageException("La imagen de referencia no es válida.");

            var mimeType = header[..^";base64".Length].ToLowerInvariant();
            if (!mimeType.StartsWith("image/"))
                throw new InvalidImageException("La referencia debe ser una imagen.");

            var base64 = reference[(comma + 1)..];
            if (base64.Length / 4L * 3 > MaxDecodedBytes)
                throw new InvalidImageException("La imagen de referencia no debe exceder los 8 MB.");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                throw new InvalidImageException("La imagen de referencia no es válida.");
            }

            using var stream = new MemoryStream(bytes);
            return await storage.UploadAsync(stream, "reference.jpg", mimeType, Folder);
        }
    }
}
