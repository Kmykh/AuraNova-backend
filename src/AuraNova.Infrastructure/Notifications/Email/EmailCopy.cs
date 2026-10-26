using AuraNova.Domain.Enums;

namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>
    /// Encabezado de cada correo según el aviso. Usa solo lo guardado en la notificación (asunto y enlace),
    /// así un reenvío días después muestra lo mismo que el correo original.
    /// </summary>
    public static class EmailCopy
    {
        /// <summary>Los asuntos tienen la forma "Título · AN-1005": el título es la parte antes del punto medio.</summary>
        public const string SubjectSeparator = " · ";

        public static EmailLayout For(NotificationType type, string? subject, string? actionUrl, string orderCode, bool isCustomOrder)
        {
            var title = string.IsNullOrWhiteSpace(subject) ? "Novedades de tu pedido" : subject.Split(SubjectSeparator)[0].Trim();
            var eyebrow = isCustomOrder ? $"Pedido personalizado · {orderCode}" : $"Pedido {orderCode}";
            var paysNow = actionUrl?.Contains("/pago/", StringComparison.Ordinal) == true;

            var (badge, tone, label) = type switch
            {
                NotificationType.OrderCreated when isCustomOrder => ("Por cotizar", EmailTone.Warning, "Ver mi pedido"),
                NotificationType.OrderCreated => ("Falta el pago", EmailTone.Warning, "Pagar mi pedido"),
                NotificationType.QuoteReady => ("Lista para aceptar", EmailTone.Warning, "Ver cotización y pagar"),
                NotificationType.PaymentReported => ("En revisión", EmailTone.Neutral, "Ver mi pedido"),
                NotificationType.PaymentConfirmed when title.StartsWith("¡Recibimos tu adelanto", StringComparison.Ordinal)
                    => ("Adelanto pagado", EmailTone.Success, "Ver mi pedido"),
                NotificationType.PaymentConfirmed => ("Pago aprobado", EmailTone.Success, "Ver mi pedido"),
                NotificationType.BalancePaid => ("Pagado por completo", EmailTone.Success, "Ver mi pedido"),
                NotificationType.PaymentRejected => ("Pago no validado", EmailTone.Danger, "Intentar de nuevo"),
                NotificationType.OrderPreparing => ("En el taller", EmailTone.Neutral, "Ver mi pedido"),
                NotificationType.OrderReady when paysNow => ("Falta el saldo", EmailTone.Warning, "Pagar el saldo"),
                NotificationType.OrderReady => ("Listo", EmailTone.Success, "Ver mi pedido"),
                NotificationType.OrderShipped => ("En camino", EmailTone.Neutral, "Ver mi pedido"),
                NotificationType.OrderDelivered => ("Entregado", EmailTone.Success, "Ver mi pedido"),
                NotificationType.OrderCancelled => ("Cancelado", EmailTone.Danger, "Ver mi pedido"),
                _ => ((string?)null, EmailTone.Neutral, "Ver mi pedido")
            };

            // El texto del botón siempre coincide con a dónde lleva
            if (paysNow && label == "Ver mi pedido") label = "Ir a pagar";
            if (!paysNow && label is "Pagar mi pedido" or "Pagar el saldo" or "Intentar de nuevo" or "Ver cotización y pagar") label = "Ver mi pedido";

            return new EmailLayout(eyebrow, title, badge, tone, label, actionUrl);
        }
    }
}
