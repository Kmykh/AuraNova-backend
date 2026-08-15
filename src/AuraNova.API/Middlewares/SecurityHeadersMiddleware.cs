using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AuraNova.API.Middlewares
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("X-XSS-Protection", "1; mode=block");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            // Content-Security-Policy could be added here if needed, but since this is an API, it's mostly consumed by clients.
            headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");

            await _next(context);
        }
    }
}
