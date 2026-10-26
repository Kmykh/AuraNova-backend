using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AuraNova.API.Configuration;
using AuraNova.Infrastructure.Notifications.Email;

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
    public class DiagnosticsAdminController(
        IOptions<SecuritySettings> securityOptions,
        IOptions<EmailSettings> emailOptions,
        IServiceProvider services) : ControllerBase
    {
        public record EmailTestRequest(string To);

        /// <summary>
        /// Envía un correo de prueba y devuelve el resultado exacto (o el motivo del error),
        /// para comprobar la configuración de correos sin hacer un pedido.
        /// </summary>
        [HttpPost("email")]
        public async Task<IActionResult> SendTestEmail([FromBody] EmailTestRequest request, CancellationToken cancellationToken)
        {
            var settings = emailOptions.Value;
            if (!settings.IsConfigured)
                return Ok(new
                {
                    ok = false,
                    provider = settings.ProviderName,
                    message = settings.UsesResend
                        ? "Faltan Email__ApiKey o Email__FromAddress en Render."
                        : "Los correos no están configurados. Agrega Email__ApiKey (Resend) y Email__FromAddress en Render."
                });

            if (string.IsNullOrWhiteSpace(request.To) || !System.Net.Mail.MailAddress.TryCreate(request.To.Trim(), out _))
                return Problem(detail: "Escribe un correo válido.", statusCode: StatusCodes.Status400BadRequest);

            try
            {
                var sender = services.GetRequiredService<IEmailSender>();
                const string text = "Este es un correo de prueba de Aura Nova. Si lo recibes, los correos automáticos funcionan.";
                await sender.SendAsync(request.To.Trim(), "Correo de prueba · Aura Nova",
                    EmailTemplate.Render("Aura Nova", "Correo de prueba", text, "Ir a la tienda", null), text, null, cancellationToken);
                return Ok(new { ok = true, provider = settings.ProviderName, message = $"Enviado a {request.To.Trim()}. Revisa la bandeja (y Spam)." });
            }
            catch (EmailSendException ex)
            {
                return Ok(new { ok = false, provider = settings.ProviderName, message = ex.Message });
            }
        }

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
