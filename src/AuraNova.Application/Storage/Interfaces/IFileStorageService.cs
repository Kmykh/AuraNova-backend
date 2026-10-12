namespace AuraNova.Application.Storage.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder);
        Task DeleteAsync(string path);

        /// <summary>
        /// Descarga un objeto del bucket. Devuelve null si no existe.
        /// </summary>
        Task<Stream?> DownloadAsync(string path);

        /// <summary>
        /// URLs para leer archivos: pública para el catálogo, firmada y temporal para archivos privados
        /// (comprobantes, constancias de envío, referencias). Los valores absolutos o data: se devuelven tal cual.
        /// Las rutas que no se pudieron resolver no aparecen en el resultado.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(IEnumerable<string> storedPaths);
    }

    public static class FileStorageServiceExtensions
    {
        public static async Task<string?> GetReadUrlAsync(this IFileStorageService storage, string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                return storedPath;

            var urls = await storage.GetReadUrlsAsync([storedPath]);
            return urls.GetValueOrDefault(storedPath);
        }
    }
}
