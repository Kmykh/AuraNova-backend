using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AuraNova.Application.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Storage
{
    public class SupabaseStorageService : IFileStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly SupabaseSettings? _settings;
        private readonly ILogger<SupabaseStorageService> _logger;
        private const string BucketName = "payment-evidence";

        public SupabaseStorageService(HttpClient httpClient, IOptions<SupabaseSettings> options, ILogger<SupabaseStorageService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (options == null)
            {
                _logger.LogWarning("IOptions<SupabaseSettings> is null.");
            }
            
            _settings = options?.Value;

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
                var endpoint = $"object/{BucketName}/{path}";

                using var content = new StreamContent(fileStream);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                var response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Supabase upload failed: {StatusCode} - {Error}", response.StatusCode, errorBody);
                    throw new Exception("Error al subir el archivo al almacenamiento.");
                }

                return path; // Return the path relative to the bucket
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception uploading file to Supabase.");
                throw;
            }
        }

        public async Task DeleteAsync(string path)
        {
            try
            {
                var endpoint = $"object/{BucketName}/{path}";
                var response = await _httpClient.DeleteAsync(endpoint);

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
    }
}
