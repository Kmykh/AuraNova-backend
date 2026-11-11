namespace AuraNova.Infrastructure.Orders
{
    /// <summary>
    /// WhatsApp del cliente: celular peruano (se guarda como antes, 9 dígitos) o, si compra desde otro país,
    /// su número con código de país ("+5491123456789", formato internacional E.164).
    /// </summary>
    public static class CustomerPhone
    {
        public static string Normalize(string? raw)
        {
            var value = raw?.Trim() ?? string.Empty;
            var digits = new string(value.Where(char.IsDigit).ToArray());

            // Perú: "987 654 321" o "+51 987 654 321"
            if (digits.Length == 9 && digits[0] == '9') return digits;
            if (digits.Length == 11 && digits.StartsWith("519")) return digits[2..];

            // Otro país: con "+" y código de país, hasta 15 dígitos
            if (value.StartsWith('+') && digits.Length is >= 8 and <= 15 && !digits.StartsWith("51"))
                return "+" + digits;

            throw new OrderValidationException(
                "Revisa tu número de WhatsApp: un celular peruano de 9 dígitos o, si estás fuera del Perú, tu número con el código de país (ej. +54 9 11 2345 6789).");
        }

        /// <summary>Número de otro país (los anteriores, sin "+", son peruanos).</summary>
        public static bool IsAbroad(string? phone) =>
            phone != null && phone.StartsWith('+') && !phone.StartsWith("+51");
    }
}
