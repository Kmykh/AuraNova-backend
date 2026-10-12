namespace AuraNova.Application.Storage.Interfaces
{
    public interface IImageOptimizer
    {
        /// <summary>
        /// Valida que el contenido sea una imagen real, corrige la orientación, elimina metadatos (EXIF/GPS),
        /// redimensiona al máximo del perfil y re-codifica en WebP.
        /// </summary>
        /// <exception cref="InvalidImageException">El contenido no es una imagen soportada o excede los límites.</exception>
        Task<OptimizedImage> OptimizeAsync(Stream input, ImageProfile profile, CancellationToken cancellationToken = default);
    }

    public sealed record ImageProfile(string Name, int MaxDimension, int Quality, bool Lossless = false)
    {
        /// <summary>Fotos del catálogo: se muestran como máximo a ~800px CSS (1600px en pantallas retina).</summary>
        public static readonly ImageProfile Catalog = new("catalog", 1600, 80);

        /// <summary>Comprobantes y evidencias: más resolución y calidad para que el texto siga legible.</summary>
        public static readonly ImageProfile Document = new("document", 2000, 85);

        /// <summary>Códigos QR: sin pérdida para no degradar la lectura.</summary>
        public static readonly ImageProfile Qr = new("qr", 1024, 100, Lossless: true);

        /// <summary>Elige el perfil según la carpeta de destino en el bucket.</summary>
        public static ImageProfile ForFolder(string folder)
        {
            var root = folder.Trim('/').Split('/')[0].ToLowerInvariant();
            return root switch
            {
                "products" or "custom-orders" => Catalog,
                "business-settings" => Qr,
                _ => Document
            };
        }
    }

    public sealed class OptimizedImage : IDisposable
    {
        public OptimizedImage(MemoryStream content, string contentType, string extension, int width, int height)
        {
            Content = content;
            ContentType = contentType;
            Extension = extension;
            Width = width;
            Height = height;
        }

        public MemoryStream Content { get; }
        public string ContentType { get; }
        public string Extension { get; }
        public int Width { get; }
        public int Height { get; }
        public long Length => Content.Length;

        public void Dispose() => Content.Dispose();
    }

    public class InvalidImageException(string message) : ArgumentException(message);
}
