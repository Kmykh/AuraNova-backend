using System;

namespace AuraNova.Domain.Entities
{
    /// <summary>
    /// Foto de la galería de un producto. La primera (SortOrder 0) es la portada y se copia en
    /// <see cref="Product.ImageUrl"/> para que tarjetas, catálogo PDF y pedidos la sigan usando.
    /// </summary>
    public class ProductImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public string Path { get; set; } = null!;
        public int SortOrder { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
