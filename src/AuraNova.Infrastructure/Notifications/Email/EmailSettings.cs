namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>
    /// SMTP para los correos automáticos (sección "Email"). Con Gmail: Host smtp.gmail.com, Port 587,
    /// Username = el correo y Password = una "contraseña de aplicación" (no la contraseña normal).
    /// Sin Host/Username/Password los correos quedan desactivados y solo se generan los WhatsApp.
    /// </summary>
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>Remitente; si está vacío se usa Username.</summary>
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "Aura Nova";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }
}
