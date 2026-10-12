using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.Orders
{
    /// <summary>
    /// Movimientos de stock sin carreras. Antes el stock se leía, se validaba y se guardaba el valor calculado:
    /// dos pedidos simultáneos del último producto leían Stock = 1 y ambos se confirmaban.
    /// Ahora el descuento es un único UPDATE condicionado; PostgreSQL bloquea la fila y el segundo pedido
    /// re-evalúa la condición con el stock ya descontado.
    /// </summary>
    public static class ProductStock
    {
        public static async Task<bool> TryReserveAsync(AppDbContext db, Guid productId, int quantity)
        {
            if (db.Database.IsRelational())
            {
                var affected = await db.Products
                    .Where(p => p.Id == productId && p.IsAvailable && p.Stock >= quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity));
                return affected == 1;
            }

            // InMemory provider (tests) does not support ExecuteUpdate
            var product = await db.Products.FindAsync(productId);
            if (product == null || !product.IsAvailable || product.Stock < quantity)
                return false;

            product.Stock -= quantity;
            return true;
        }

        public static async Task ReleaseAsync(AppDbContext db, Guid productId, int quantity)
        {
            if (db.Database.IsRelational())
            {
                await db.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity));
                return;
            }

            var product = await db.Products.FindAsync(productId);
            if (product != null)
                product.Stock += quantity;
        }
    }
}
