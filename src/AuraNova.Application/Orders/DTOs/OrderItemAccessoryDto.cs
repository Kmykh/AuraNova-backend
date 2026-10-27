namespace AuraNova.Application.Orders.DTOs
{
    /// <summary>Accesorio elegido en un pedido con el precio que tenía al comprar.</summary>
    public class OrderItemAccessoryDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
