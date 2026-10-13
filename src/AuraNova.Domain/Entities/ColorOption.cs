namespace AuraNova.Domain.Entities
{
    /// <summary>
    /// Color de la paleta del negocio: el nombre es lo que se guarda en productos y pedidos,
    /// el código se usa para dibujar la muestra. "multicolor" representa el surtido.
    /// </summary>
    public class ColorOption
    {
        public string Name { get; set; } = string.Empty;
        public string Hex { get; set; } = string.Empty;
    }
}
