using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Notifications.Email
{
    public interface IEmailSender
    {
        /// <param name="idempotencyKey">Mismo valor en un reintento = el proveedor no envía dos veces.</param>
        Task SendAsync(string to, string subject, string html, string plainText, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    }

    /// <summary>El proveedor rechazó o no pudo enviar el correo; el mensaje explica por qué.</summary>
    public class EmailSendException(string message, Exception? inner = null) : Exception(message, inner);

    /// <summary>Envío por la API de Resend (HTTPS, puerto 443): funciona aunque el servidor bloquee SMTP.</summary>
    public class ResendEmailSender(HttpClient http, IOptions<EmailSettings> options) : IEmailSender
    {
        private readonly EmailSettings _settings = options.Value;

        public async Task SendAsync(string to, string subject, string html, string plainText, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            // Una key mal pegada haría fallar el encabezado con un error que incluye la key: se avisa sin mostrarla
            if (!_settings.ApiKeyLooksValid)
                throw new EmailSendException("La API key de Resend no es válida: debe empezar con \"re_\" y no tener espacios (revisa Email__ApiKey en Render).");

            var body = new Dictionary<string, object>
            {
                ["from"] = $"{_settings.FromName} <{_settings.FromAddress}>",
                ["to"] = new[] { to },
                ["subject"] = subject,
                ["html"] = html,
                ["text"] = plainText
            };
            if (!string.IsNullOrWhiteSpace(_settings.ReplyTo))
                body["reply_to"] = _settings.ReplyTo;

            using var request = new HttpRequestMessage(HttpMethod.Post, "emails") { Content = JsonContent.Create(body) };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new EmailSendException("No se pudo conectar con Resend.", ex);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return;

                // Resend explica el problema en "message" (p. ej. dominio sin verificar o API key inválida)
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                string? message = null;
                try { message = JsonDocument.Parse(detail).RootElement.GetProperty("message").GetString(); }
                catch (Exception) { /* respuesta sin JSON */ }
                throw new EmailSendException($"Resend respondió {(int)response.StatusCode}: {message ?? detail}");
            }
        }
    }

    /// <summary>Envío por SMTP con STARTTLS (Gmail, Outlook, etc.). No funciona en Render gratis: bloquea los puertos SMTP.</summary>
    public class SmtpEmailSender(IOptions<EmailSettings> options) : IEmailSender
    {
        private readonly EmailSettings _settings = options.Value;

        public async Task SendAsync(string to, string subject, string html, string plainText, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.Username : _settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = plainText,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(to));
            if (!string.IsNullOrWhiteSpace(_settings.ReplyTo))
                message.ReplyToList.Add(new MailAddress(_settings.ReplyTo));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, System.Text.Encoding.UTF8, "text/html"));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                Timeout = 20000
            };
            try
            {
                await client.SendMailAsync(message, cancellationToken);
            }
            catch (Exception ex) when (ex is SmtpException or IOException or System.Net.Sockets.SocketException)
            {
                throw new EmailSendException(
                    $"SMTP falló ({ex.Message}). Si el backend está en Render gratis, los puertos SMTP están bloqueados: usa Resend (Email__ApiKey).", ex);
            }
        }
    }
}
