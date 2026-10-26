using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Notifications.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string html, string plainText, CancellationToken cancellationToken = default);
    }

    /// <summary>Envío por SMTP con STARTTLS (Gmail, Outlook, Brevo, etc.).</summary>
    public class SmtpEmailSender(IOptions<EmailSettings> options) : IEmailSender
    {
        private readonly EmailSettings _settings = options.Value;

        public async Task SendAsync(string to, string subject, string html, string plainText, CancellationToken cancellationToken = default)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.Username : _settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = plainText,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(to));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, System.Text.Encoding.UTF8, "text/html"));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                Timeout = 20000
            };
            await client.SendMailAsync(message, cancellationToken);
        }
    }
}
