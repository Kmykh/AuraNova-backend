namespace AuraNova.Infrastructure.Storage
{
    public class SupabaseSettings
    {
        public string Url { get; set; } = null!;
        public string ServiceRoleKey { get; set; } = null!;

        /// <summary>Bucket público (catálogo, QR, assets). Se mantiene el nombre histórico por compatibilidad.</summary>
        public string Bucket { get; set; } = "payment-evidence";

        /// <summary>Bucket privado para archivos de clientes. Solo se accede con URLs firmadas temporales.</summary>
        public string PrivateBucket { get; set; } = "customer-files";

        /// <summary>Carpetas que se guardan en el bucket privado.</summary>
        public string[] PrivateFolders { get; set; } = ["payment-evidence", "shipping-evidence", "custom-orders"];

        /// <summary>Vigencia de las URLs firmadas que se entregan al panel y al tracking.</summary>
        public int SignedUrlSeconds { get; set; } = 3600;

        /// <summary>
        /// Base pública opcional (p. ej. un CDN propio: https://cdn.auranova.pe). Si se omite se usa
        /// {Url}/storage/v1/object/public/{Bucket}.
        /// </summary>
        public string? PublicBaseUrl { get; set; }

        /// <summary>
        /// max-age que se guarda con cada objeto. Los nombres son GUID únicos (nunca se sobrescriben),
        /// así que es seguro cachearlos un año en navegador y CDN.
        /// </summary>
        public int CacheControlSeconds { get; set; } = 31536000;
    }
}
