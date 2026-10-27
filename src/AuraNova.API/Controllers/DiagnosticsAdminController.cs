using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AuraNova.API.Configuration;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Notifications.Email;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.API.Controllers
{
    /// <summary>
    /// Permite comprobar en producción que el rate limiting por IP ve la IP real del cliente
    /// (y no la del proxy de Render, que haría que todos compartan el mismo límite).
    /// </summary>
    [ApiController]
    [Route("api/admin/diagnostics")]
    [Authorize(Roles = "SuperAdmin")] // Herramientas del desarrollador, no del equipo que atiende pedidos
    [EnableRateLimiting("admin_policy")]
    public class DiagnosticsAdminController(
        IOptions<SecuritySettings> securityOptions,
        IOptions<EmailSettings> emailOptions,
        IServiceProvider services,
        AppDbContext db,
        IConfiguration configuration) : ControllerBase
    {
        public record EmailTestRequest(string To);

        /// <summary>
        /// Estado de los correos para el panel: si están configurados, con qué remitente y los últimos envíos
        /// con su error. Nunca devuelve la API key.
        /// </summary>
        [HttpGet("email")]
        public async Task<IActionResult> GetEmailStatus(CancellationToken cancellationToken)
        {
            var settings = emailOptions.Value;
            var notificationsDisabled = bool.TryParse(configuration["NotificationSettings:Enabled"], out var enabled) && !enabled;

            string? problem = null;
            if (notificationsDisabled)
                problem = "Las notificaciones están desactivadas (NotificationSettings__Enabled=false en Render).";
            else if (settings.UsesResend && !settings.ApiKeyLooksValid)
                problem = "La API key de Resend no es válida: debe empezar con \"re_\" (revisa Email__ApiKey en Render).";
            else if (!settings.IsConfigured)
                problem = settings.UsesResend
                    ? "Falta Email__FromAddress en Render."
                    : "Los correos no están configurados: agrega Email__ApiKey (Resend) y Email__FromAddress en Render.";

            var recent = await db.Set<Notification>()
                .AsNoTracking()
                .Where(n => n.Channel == NotificationChannel.Email)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .Select(n => new
                {
                    n.Id,
                    OrderCode = n.Order != null ? n.Order.OrderCode : null,
                    n.Type,
                    n.Status,
                    n.Recipient,
                    n.ErrorMessage,
                    n.CreatedAt,
                    n.SentAt
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                configured = problem == null,
                provider = settings.ProviderName,
                fromAddress = string.IsNullOrWhiteSpace(settings.FromAddress) ? null : settings.FromAddress,
                fromName = settings.FromName,
                replyTo = string.IsNullOrWhiteSpace(settings.ReplyTo) ? null : settings.ReplyTo,
                problem,
                recent
            });
        }

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
                const string text = "Hola,\n\nEste es un correo de prueba. Si lo recibes, los correos automáticos a tus clientes funcionan.\n\nGracias por elegir Aura Nova";
                var site = services.GetService<IOptions<AuraNova.Infrastructure.Notifications.PublicSiteSettings>>()?.Value.BaseUrl
                    ?? new AuraNova.Infrastructure.Notifications.PublicSiteSettings().BaseUrl;
                var html = EmailTemplate.Render(new EmailBrand("Aura Nova", site, null),
                    new EmailLayout("Prueba", "Correo de prueba", "Funciona", EmailTone.Success, "Ir a la tienda", site), text);
                await sender.SendAsync(request.To.Trim(), "Correo de prueba · Aura Nova", html, text, null, cancellationToken);
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
