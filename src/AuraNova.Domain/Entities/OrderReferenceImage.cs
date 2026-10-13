using System;

namespace AuraNova.Domain.Entities
{
    /// <summary>
    /// Imagen que el cliente envía como modelo o inspiración para un pedido personalizado.
    /// El archivo vive en el bucket privado; aquí solo se guarda su ruta.
    /// </summary>
    public class OrderReferenceImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public string Path { get; set; } = null!;
        public int SortOrder { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
