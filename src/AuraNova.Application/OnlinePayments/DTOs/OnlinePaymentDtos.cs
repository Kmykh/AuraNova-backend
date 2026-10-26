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
}
