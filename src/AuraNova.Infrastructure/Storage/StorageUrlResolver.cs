using AuraNova.Application.Storage.Interfaces;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Storage
{
    public class StorageUrlResolver : IStorageUrlResolver
    {
        private readonly string? _publicBase;

        public StorageUrlResolver(IOptions<SupabaseSettings> options)
        {
            var settings = options.Value;
            if (!string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
            {
                _publicBase = settings.PublicBaseUrl.TrimEnd('/');
            }
            else if (!string.IsNullOrWhiteSpace(settings.Url))
            {
                var bucket = string.IsNullOrWhiteSpace(settings.Bucket) ? "payment-evidence" : settings.Bucket;
                _publicBase = $"{settings.Url.TrimEnd('/')}/storage/v1/object/public/{bucket}";
            }
        }

        public string? ToPublicUrl(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl) || _publicBase == null || IsAbsoluteOrInline(pathOrUrl))
                return pathOrUrl;

            return $"{_publicBase}/{pathOrUrl.TrimStart('/')}";
        }

        public string? ToStoragePath(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl) || _publicBase == null)
                return pathOrUrl;

            var prefix = _publicBase + "/";
            return pathOrUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? pathOrUrl[prefix.Length..]
                : pathOrUrl;
        }

        internal static bool IsAbsoluteOrInline(string value) =>
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("blob:", StringComparison.OrdinalIgnoreCase);
    }
}
