namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>
    /// Correos automáticos (sección "Email").
    /// - Resend (recomendado, por HTTPS): ApiKey + FromAddress de un dominio verificado (p. ej. pedidos@auranovaoficial.com).
    ///   Render gratis bloquea los puertos SMTP (25, 465 y 587) desde sept. 2025: por eso SMTP no sale desde ahí.
    /// - SMTP: Host, Port, Username y Password (Gmail con "contraseña de aplicación"); solo en servidores que permitan SMTP.
    /// Sin configuración los correos quedan desactivados y solo se generan los WhatsApp.
    /// </summary>
    public class EmailSettings
    {
        /// <summary>"resend" o "smtp". Vacío: Resend si hay ApiKey; si no, SMTP.</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>API key de Resend (re_…).</summary>
        public string ApiKey { get; set; } = string.Empty;

        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>Remitente; con SMTP, si está vacío se usa Username.</summary>
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "Aura Nova";

        /// <summary>A dónde llegan las respuestas de los clientes (p. ej. el Gmail del negocio).</summary>
        public string ReplyTo { get; set; } = string.Empty;

        public bool UsesResend =>
            string.Equals(Provider, "resend", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(ApiKey));

        public bool IsConfigured => UsesResend
            ? !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromAddress)
            : !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

        public string ProviderName => UsesResend ? "Resend" : "SMTP";

        /// <summary>Las API keys de Resend empiezan con "re_" y no tienen espacios.</summary>
        public bool ApiKeyLooksValid => ApiKey.StartsWith("re_", StringComparison.Ordinal) && !ApiKey.Any(char.IsWhiteSpace);

        /// <summary>
        /// Quita espacios, saltos de línea y comillas que suelen colarse al pegar los valores en Render
        /// (una key con un espacio al final rompe el encabezado y el correo ni llega a Resend).
        /// </summary>
        public void Normalize()
        {
            static string Clean(string value) => value.Trim().Trim('"', '\'').Trim();
            Provider = Clean(Provider);
            ApiKey = Clean(ApiKey);
            Host = Clean(Host);
            Username = Clean(Username);
            FromAddress = Clean(FromAddress);
            FromName = Clean(FromName);
            ReplyTo = Clean(ReplyTo);
        }
    }
}
