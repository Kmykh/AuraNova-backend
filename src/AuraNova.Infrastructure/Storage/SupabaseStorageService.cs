using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AuraNova.Application.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Storage
{
    public class SupabaseStorageService : IFileStorageService
    {
        /// <summary>
        /// Prefijo lógico de las rutas guardadas en BD para archivos del bucket privado
        /// (p. ej. "private/payment-evidence/{orderId}/{guid}.webp"). Las rutas sin prefijo son del bucket público.
        /// </summary>
        public const string PrivatePrefix = "private/";

        private readonly HttpClient _httpClient;
        private readonly SupabaseSettings? _settings;
        private readonly ILogger<SupabaseStorageService> _logger;
        private readonly IStorageUrlResolver _urlResolver;
        private readonly string _bucketName;
        private readonly string _privateBucketName;
        private readonly HashSet<string> _privateFolders;

        public SupabaseStorageService(HttpClient httpClient, IOptions<SupabaseSettings> options, ILogger<SupabaseStorageService> logger,
            IStorageUrlResolver urlResolver)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _urlResolver = urlResolver;

            if (options == null)
            {
                _logger.LogWarning("IOptions<SupabaseSettings> is null.");
            }

            _settings = options?.Value;
            _bucketName = string.IsNullOrWhiteSpace(_settings?.Bucket) ? "payment-evidence" : _settings.Bucket;
            _privateBucketName = string.IsNullOrWhiteSpace(_settings?.PrivateBucket) ? "customer-files" : _settings.PrivateBucket;
            _privateFolders = new HashSet<string>(_settings?.PrivateFolders ?? [], StringComparer.OrdinalIgnoreCase);

            // Remove trailing slash from URL if present
            if (_settings != null && !string.IsNullOrWhiteSpace(_settings.Url))
            {
                var baseUrl = _settings.Url.TrimEnd('/');
                _httpClient.BaseAddress = new Uri($"{baseUrl}/storage/v1/");

                if (!string.IsNullOrWhiteSpace(_settings.ServiceRoleKey))
                {
                    // Supabase requires the service role key as Authorization Bearer AND apikey
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
                    _httpClient.DefaultRequestHeaders.Add("apikey", _settings.ServiceRoleKey);
                }
            }
            else
            {
                _logger.LogWarning("SupabaseSettings or Supabase Url is null/empty. Storage service will not work properly.");
            }
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder)
        {
            try
            {
                // Ensure unique file name
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var uniqueName = $"{Guid.NewGuid()}{extension}";

                // Supabase path: object/bucketName/folder/filename
                var path = $"{folder}/{uniqueName}";
                var isPrivate = IsPrivateFolder(folder);
                var endpoint = $"object/{(isPrivate ? _privateBucketName : _bucketName)}/{path}";

                using var content = new StreamContent(fileStream);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
                // Sin esta cabecera Supabase guarda el objeto con "no-cache" y el navegador lo revalida en cada visita.
                // El nombre es un GUID nuevo en cada subida, por lo que el contenido de una URL nunca cambia.
                var maxAge = _settings?.CacheControlSeconds ?? 31536000;
                request.Headers.TryAddWithoutValidation("cache-control", $"max-age={maxAge}");
                request.Headers.TryAddWithoutValidation("x-upsert", "false");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Supabase upload failed: {StatusCode} - {Error}", response.StatusCode, errorBody);
                    throw new Exception("Error al subir el archivo al almacenamiento.");
                }

                return isPrivate ? PrivatePrefix + path : path; // Return the path relative to the bucket
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception uploading file to Supabase.");
                throw;
            }
        }

        public async Task<Stream?> DownloadAsync(string path)
        {
            var (bucket, key) = Locate(path);
            var response = await _httpClient.GetAsync($"object/{bucket}/{key}");

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                _logger.LogWarning("Supabase object not found. Path: {Path}", path);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer);
            buffer.Position = 0;
            return buffer;
        }

        public async Task DeleteAsync(string path)
        {
            try
            {
                var (bucket, key) = Locate(path);
                var response = await _httpClient.DeleteAsync($"object/{bucket}/{key}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Supabase delete failed: {StatusCode} - {Error}. Path: {Path}", response.StatusCode, errorBody, path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting file from Supabase. Path: {Path}", path);
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(IEnumerable<string> storedPaths)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var privatePaths = new List<string>();

            foreach (var stored in storedPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal))
            {
                if (stored.StartsWith(PrivatePrefix, StringComparison.OrdinalIgnoreCase))
                    privatePaths.Add(stored);
                else if (_urlResolver.ToPublicUrl(stored) is { } publicUrl)
                    result[stored] = publicUrl;
            }

            if (privatePaths.Count == 0 || _settings == null || string.IsNullOrWhiteSpace(_settings.Url))
                return result;

            try
            {
                // Una sola llamada para firmar todas las rutas de la respuesta
                var response = await _httpClient.PostAsJsonAsync($"object/sign/{_privateBucketName}", new
                {
                    expiresIn = Math.Max(60, _settings.SignedUrlSeconds),
                    paths = privatePaths.Select(p => p[PrivatePrefix.Length..]).ToArray()
                });

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Supabase sign failed: {StatusCode} - {Error}", response.StatusCode, errorBody);
                    return result;
                }

                var signed = await response.Content.ReadFromJsonAsync<List<SignedUrlResult>>() ?? [];
                var storageBase = $"{_settings.Url.TrimEnd('/')}/storage/v1";
                foreach (var item in signed.Where(s => s.SignedURL != null && s.Path != null))
                {
                    result[PrivatePrefix + item.Path] = storageBase + item.SignedURL;
                }
            }
            catch (Exception ex)
            {
                // Un fallo al firmar no debe tumbar el detalle del pedido; solo faltará el enlace
                _logger.LogError(ex, "Exception signing private file URLs.");
            }

            return result;
        }

        private bool IsPrivateFolder(string folder) =>
            _privateFolders.Contains(folder.Trim('/').Split('/')[0]);

        /// <summary>
        /// Bucket y ruta real de un valor guardado en BD. Acepta rutas relativas, rutas con prefijo
        /// "private/" y URLs públicas completas del bucket público.
        /// </summary>
        private (string Bucket, string Key) Locate(string stored)
        {
            if (stored.StartsWith(PrivatePrefix, StringComparison.OrdinalIgnoreCase))
                return (_privateBucketName, stored[PrivatePrefix.Length..]);

            var marker = $"/object/public/{_bucketName}/";
            var index = stored.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return (_bucketName, index >= 0 ? stored[(index + marker.Length)..] : stored.TrimStart('/'));
        }

        private sealed class SignedUrlResult
        {
            public string? Path { get; set; }
            public string? SignedURL { get; set; }
            public string? Error { get; set; }
        }
    }
}
