using System.Net;
using System.Security.Claims;

namespace AuraNova.API.Configuration
{
    /// <summary>
    /// Clave de partición para el rate limiting. Sin esto todos los clientes compartían un único contador
    /// y bastaban 5 intentos de login de un atacante para bloquear al administrador.
    /// </summary>
    public static class ClientIp
    {
        public static string Resolve(HttpContext context, string? trustedHeader)
        {
            if (!string.IsNullOrWhiteSpace(trustedHeader) &&
                context.Request.Headers.TryGetValue(trustedHeader, out var headerValue) &&
                IPAddress.TryParse(headerValue.ToString().Split(',')[0].Trim(), out var fromHeader))
            {
                return fromHeader.ToString();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        /// <summary>Para endpoints de admin: por usuario autenticado, o por IP si no hay usuario.</summary>
        public static string ResolveUserOrIp(HttpContext context, string? trustedHeader)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            return string.IsNullOrEmpty(userId) ? $"ip:{Resolve(context, trustedHeader)}" : $"user:{userId}";
        }
    }
}
