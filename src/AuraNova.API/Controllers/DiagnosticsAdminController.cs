using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AuraNova.API.Configuration;

namespace AuraNova.API.Controllers
{
    /// <summary>
    /// Permite comprobar en producción que el rate limiting por IP ve la IP real del cliente
    /// (y no la del proxy de Render, que haría que todos compartan el mismo límite).
    /// </summary>
    [ApiController]
    [Route("api/admin/diagnostics")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class DiagnosticsAdminController(IOptions<SecuritySettings> securityOptions) : ControllerBase
    {
        [HttpGet("client-ip")]
        public IActionResult GetClientIp()
        {
            var settings = securityOptions.Value;
            string? Header(string name) => Request.Headers.TryGetValue(name, out var value) ? value.ToString() : null;

            return Ok(new
            {
                rateLimitKey = ClientIp.Resolve(HttpContext, settings.ClientIpHeader),
                remoteIpAfterForwarding = HttpContext.Connection.RemoteIpAddress?.ToString(),
                forwardLimit = settings.ForwardedHeadersForwardLimit,
                clientIpHeader = settings.ClientIpHeader,
                xForwardedForRemaining = Header("X-Forwarded-For"),
                xOriginalFor = Header("X-Original-For"),
                cfConnectingIp = Header("CF-Connecting-IP"),
                trueClientIp = Header("True-Client-IP"),
                scheme = Request.Scheme
            });
        }
    }
}
