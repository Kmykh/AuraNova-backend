namespace AuraNova.API.Configuration
{
    public class SecuritySettings
    {
        public string[] AllowedOrigins { get; set; } = [];
        public int UploadMaxSizeMb { get; set; } = 5;

        /// <summary>
        /// Cuántas entradas de X-Forwarded-For (desde la derecha) añaden proxies de confianza.
        /// Render añade su propia IP al final, por lo que la IP real del cliente es la penúltima (2).
        /// Verificar con GET /api/admin/diagnostics/client-ip después de cada cambio de infraestructura.
        /// </summary>
        public int ForwardedHeadersForwardLimit { get; set; } = 2;

        /// <summary>
        /// Cabecera opcional con la IP del cliente puesta por un proxy de confianza (p. ej. "CF-Connecting-IP"
        /// si hay Cloudflare delante). Si está vacía se usa la IP resuelta por ForwardedHeaders.
        /// </summary>
        public string? ClientIpHeader { get; set; }
    }
}
