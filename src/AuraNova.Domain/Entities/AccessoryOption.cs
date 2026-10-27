namespace AuraNova.Domain.Entities
{
    /// <summary>
    /// Accesorio que el negocio ofrece para sus ramos (luces, mariposas, perlas…) con su precio extra.
    /// La clave no cambia aunque se renombre: es lo que guardan los productos.
    /// </summary>
    public class AccessoryOption
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool Active { get; set; } = true;
    }

    /// <summary>Accesorio elegido en un pedido, con el precio que tenía en ese momento.</summary>
    public class OrderItemAccessory
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
