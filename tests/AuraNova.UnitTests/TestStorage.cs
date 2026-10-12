using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Storage.Interfaces;

namespace AuraNova.UnitTests
{
    /// <summary>Storage en memoria: rutas públicas → URL pública de prueba, rutas "private/" → URL "firmada" falsa.</summary>
    internal class TestStorage : IFileStorageService
    {
        public const string SignedBase = "https://signed.test/";

        public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder) =>
            Task.FromResult($"{folder}/{fileName}");

        public Task DeleteAsync(string path) => Task.CompletedTask;

        public Task<Stream?> DownloadAsync(string path) => Task.FromResult<Stream?>(null);

        public Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(IEnumerable<string> storedPaths) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(storedPaths.Distinct().ToDictionary(
                p => p,
                p => p.StartsWith("private/") ? SignedBase + p : TestUrlResolver.Instance.ToPublicUrl(p)!));
    }
}
