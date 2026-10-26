using System;

namespace AuraNova.Domain.Entities
{
    /// <summary>
    /// Registro de cada aviso (webhook) de Mercado Pago: permite ver en el panel si llegan bien
    /// o si se rechazan (por ejemplo, por una clave secreta mal configurada). Se guardan 30 días.
    /// </summary>
    public class PaymentWebhookEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>Tópico del aviso (order, payment…).</summary>
        public string? Topic { get; set; }
        /// <summary>data.id: el id de la order de Mercado Pago.</summary>
        public string? ResourceId { get; set; }
        /// <summary>processed, ignored, invalid_signature o error.</summary>
        public string Result { get; set; } = null!;
        public string? Detail { get; set; }
    }
}
