using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.OnlinePayments.DTOs
{
    /// <summary>
    /// Pago en la página con Mercado Pago. El monto lo decide el servidor: el 50% o el total en el primer pago,
    /// y el saldo en el segundo. El navegador solo envía el token que generó MercadoPago.js.
    /// </summary>
    public class CreateOnlinePaymentRequest
    {
        /// <summary>"card" o "yape".</summary>
        [Required, RegularExpression("^(card|yape)$")]
        public string Method { get; set; } = null!;

        /// <summary>Token de un solo uso de la tarjeta (Card Payment Brick) o de Yape (mp.yape().create()).</summary>
        [Required, MaxLength(200)]
        public string Token { get; set; } = null!;

        /// <summary>Tarjeta: visa, master, amex, debvisa… (formData.payment_method_id).</summary>
        [MaxLength(40)]
        public string? PaymentMethodId { get; set; }

        /// <summary>Tarjeta: credit_card o debit_card (additionalData.paymentTypeId).</summary>
        [MaxLength(40)]
        public string? PaymentTypeId { get; set; }

        [Range(1, 36)]
        public int? Installments { get; set; }

        /// <summary>true = paga el total; false = adelanto del 50%. Se ignora si ya pagó el adelanto (se cobra el saldo).</summary>
        public bool PayFull { get; set; } = true;

        [EmailAddress, MaxLength(150)]
        public string? PayerEmail { get; set; }

        [MaxLength(20)]
        public string? IdentificationType { get; set; }

        [MaxLength(20)]
        public string? IdentificationNumber { get; set; }

        /// <summary>Yape: celular asociado (9 dígitos).</summary>
        [MaxLength(15)]
        public string? Phone { get; set; }

        /// <summary>Identificador del dispositivo que genera security.js de Mercado Pago (MP_DEVICE_SESSION_ID).</summary>
        [MaxLength(500), RegularExpression("^[A-Za-z0-9._:-]+$")]
        public string? DeviceId { get; set; }
    }

    public class OnlinePaymentResult
    {
        /// <summary>approved, rejected o pending.</summary>
        public string Status { get; set; } = null!;
        public string Message { get; set; } = null!;
        public decimal AmountCharged { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string OrderStatus { get; set; } = null!;
    }

    /// <summary>Error de validación que el cliente puede leer (monto, estado del pedido, datos faltantes).</summary>
    public class OnlinePaymentException(string message) : Exception(message);

    /// <summary>Devolución desde el panel. Sin monto se devuelve todo lo que queda de ese cobro.</summary>
    public class RefundOnlinePaymentRequest
    {
        [Range(0.01, 100000)]
        public decimal? Amount { get; set; }

        [MaxLength(250)]
        public string? Reason { get; set; }
    }

    public class RefundOnlinePaymentResult
    {
        public decimal Refunded { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string Message { get; set; } = null!;
    }

    /// <summary>Un cobro en línea para el reporte de pagos del panel.</summary>
    public class OnlinePaymentReportRow
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string? CustomerName { get; set; }
        public string MethodId { get; set; } = null!;
        public string? MethodType { get; set; }
        public decimal Amount { get; set; }
        public decimal RefundedAmount { get; set; }
        public bool IsDeposit { get; set; }
        public string Status { get; set; } = null!;
        public string? StatusDetail { get; set; }
        public bool Approved { get; set; }
        public string? ExternalOrderId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
    }

    public class OnlinePaymentReport
    {
        public DateTimeOffset From { get; set; }
        public DateTimeOffset To { get; set; }
        /// <summary>Suma de cobros aprobados.</summary>
        public decimal Collected { get; set; }
        public decimal Refunded { get; set; }
        public decimal Net { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int PendingCount { get; set; }
        public IReadOnlyList<OnlinePaymentReportRow> Rows { get; set; } = [];
    }

    public class PaymentWebhookEventResponse
    {
        public DateTimeOffset ReceivedAt { get; set; }
        public string? Topic { get; set; }
        public string? ResourceId { get; set; }
        public string Result { get; set; } = null!;
        public string? Detail { get; set; }
    }
}
