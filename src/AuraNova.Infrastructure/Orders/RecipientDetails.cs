using AuraNova.Application.Orders.DTOs;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>Quién recibe el pedido y datos para la agencia, limpios y validados.</summary>
    public sealed record RecipientDetails(string? Name, string? Phone, string? Document, string? Reference, string? Agency)
    {
        private static readonly string[] Agencies = ["Shalom", "Olva"];

        public static RecipientDetails From(CreateOrderDeliveryRequest? delivery)
        {
            if (delivery == null)
                return new RecipientDetails(null, null, null, null, null);

            var phone = Digits(delivery.RecipientPhone);
            if (phone != null && phone.Length != 9)
                throw new OrderValidationException("El celular de quien recibe debe tener 9 dígitos.");

            // DNI: 8 dígitos; carné de extranjería: hasta 12 caracteres
            var document = delivery.RecipientDocument == null
                ? null
                : new string(delivery.RecipientDocument.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (string.IsNullOrEmpty(document)) document = null;
            if (document != null && (document.Length < 8 || document.Length > 12))
                throw new OrderValidationException("El documento de quien recibe debe ser un DNI de 8 dígitos o un CE válido.");

            var agency = Clean(delivery.PreferredAgency);
            if (agency != null)
                agency = Agencies.FirstOrDefault(a => string.Equals(a, agency, StringComparison.OrdinalIgnoreCase))
                         ?? throw new OrderValidationException("Elige una agencia válida (Shalom u Olva).");

            return new RecipientDetails(Clean(delivery.RecipientName), phone, document, Clean(delivery.DeliveryReference), agency);
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Digits(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length == 0 ? null : digits;
        }
    }
}
