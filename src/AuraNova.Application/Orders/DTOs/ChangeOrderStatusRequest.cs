using System.ComponentModel.DataAnnotations;

namespace AuraNova.Application.Orders.DTOs
{
    public class ChangeOrderStatusRequest
    {
        /// <summary>Nombre del estado ("Delivered") o su número (8): el panel enviaba el número y la petición fallaba.</summary>
        [Required(ErrorMessage = "El estado es obligatorio.")]
        [System.Text.Json.Serialization.JsonConverter(typeof(StringOrNumberConverter))]
        public string Status { get; set; } = null!;

        [MaxLength(2000)]
        public string? Comment { get; set; }
    }

    /// <summary>Lee un texto o un número JSON como texto.</summary>
    public sealed class StringOrNumberConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        public override string? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
            reader.TokenType == System.Text.Json.JsonTokenType.Number
                ? reader.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetString();

        public override void Write(System.Text.Json.Utf8JsonWriter writer, string value, System.Text.Json.JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
