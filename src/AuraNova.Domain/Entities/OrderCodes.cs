using System;
using System.Linq;
using System.Security.Cryptography;

namespace AuraNova.Domain.Entities
{
    /// <summary>
    /// Formatos de los códigos que ve el cliente.
    /// - Número de pedido "AN-1001": corto y fácil de dictar por WhatsApp. No da acceso a nada.
    /// - Código de seguimiento "K7M3-Q9XA": secreto, aleatorio (CSPRNG) y suficiente por sí solo para ver el pedido.
    ///   31^8 ≈ 8,5·10^11 combinaciones; con el rate limit por IP no es viable adivinarlo.
    /// </summary>
    public static class OrderCodes
    {
        public const string OrderNumberPrefix = "AN-";

        /// <summary>Sin 0/O, 1/I/L: no hay caracteres que se confundan al leerlos o dictarlos.</summary>
        public const string TrackingAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public const int TrackingLength = 8;

        public static string FormatOrderNumber(long number) => $"{OrderNumberPrefix}{number}";

        public static string NewTrackingCode()
        {
            var raw = RandomNumberGenerator.GetString(TrackingAlphabet, TrackingLength);
            return $"{raw[..4]}-{raw[4..]}";
        }

        /// <summary>
        /// Acepta lo que escriba o pegue el cliente ("k7m3q9xa", "K7M3 - Q9XA", un enlace completo…) y devuelve
        /// el formato canónico "K7M3-Q9XA", o null si no es un código válido.
        /// </summary>
        public static string? NormalizeTrackingCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // Si pegan el enlace completo, el código es el último segmento
            var lastSegment = input.Trim().TrimEnd('/').Split('/', '?', '=').Last();
            var compact = new string(lastSegment.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

            if (compact.Length != TrackingLength || compact.Any(c => !TrackingAlphabet.Contains(c)))
                return null;

            return $"{compact[..4]}-{compact[4..]}";
        }
    }
}
