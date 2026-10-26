namespace AuraNova.Domain.Enums
{
    public enum PaymentMethod
    {
        /// <summary>Yape con QR y comprobante (flujo anterior).</summary>
        Yape,
        /// <summary>Tarjeta o Yape cobrados en la página con Mercado Pago.</summary>
        MercadoPago
    }
}
