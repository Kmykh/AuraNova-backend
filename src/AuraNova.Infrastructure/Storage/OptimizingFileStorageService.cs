using AuraNova.Application.Storage.Interfaces;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Storage
{
    /// <summary>
    /// Decorador de <see cref="IFileStorageService"/>: toda imagen que se sube (productos, comprobantes, QR,
    /// evidencias de envío, referencias de pedidos personalizados) se valida y se convierte a WebP optimizado
    /// antes de llegar al bucket. Los archivos que no son imágenes pasan sin cambios.
    /// </summary>
    public class OptimizingFileStorageService(
        IFileStorageService inner,
        IImageOptimizer optimizer,
        ILogger<OptimizingFileStorageService> logger) : IFileStorageService
    {
        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder)
        {
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return await inner.UploadAsync(fileStream, fileName, contentType, folder);

            var profile = ImageProfile.ForFolder(folder);
            using var optimized = await optimizer.OptimizeAsync(fileStream, profile);

            logger.LogInformation(
                "Imagen optimizada ({Profile}) para {Folder}: {Width}x{Height}, {Bytes} bytes",
                profile.Name, folder, optimized.Width, optimized.Height, optimized.Length);

            var optimizedName = Path.ChangeExtension(fileName, optimized.Extension);
            return await inner.UploadAsync(optimized.Content, optimizedName, optimized.ContentType, folder);
        }

        public Task DeleteAsync(string path) => inner.DeleteAsync(path);

        public Task<Stream?> DownloadAsync(string path) => inner.DownloadAsync(path);

        public Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(IEnumerable<string> storedPaths) =>
            inner.GetReadUrlsAsync(storedPaths);
    }
}
