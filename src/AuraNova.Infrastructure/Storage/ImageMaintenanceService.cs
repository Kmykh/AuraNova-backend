using AuraNova.Application.Storage.Interfaces;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Storage
{
    /// <summary>
    /// Tareas de una sola vez para sanear las imágenes que se subieron antes de la optimización automática.
    /// <paramref name="rawStorage"/> debe ser el storage SIN el decorador de optimización, para no re-codificar dos veces.
    /// </summary>
    public class ImageMaintenanceService(
        AppDbContext db,
        IFileStorageService rawStorage,
        IImageOptimizer optimizer,
        IReferenceImageService referenceImages,
        IStorageUrlResolver urlResolver,
        ILogger<ImageMaintenanceService> logger) : IImageMaintenanceService
    {
        public async Task<ImageMaintenanceReport> CleanupOrphanProductImagesAsync(bool dryRun, int minAgeHours = 24, CancellationToken cancellationToken = default)
        {
            var report = new ImageMaintenanceReport { DryRun = dryRun };

            // Rutas en uso, normalizadas por si algún producto guardó la URL pública completa
            var inUse = (await db.Products
                    .Where(p => p.ImageUrl != null && p.ImageUrl != "")
                    .Select(p => p.ImageUrl!)
                    .ToListAsync(cancellationToken))
                .Select(url => urlResolver.ToStoragePath(url)!.TrimStart('/'))
                .ToHashSet(StringComparer.Ordinal);

            var cutoff = DateTimeOffset.UtcNow.AddHours(-Math.Max(0, minAgeHours));
            var files = await rawStorage.ListAsync("products");

            foreach (var file in files)
            {
                if (inUse.Contains(file.Path))
                    continue;

                if (file.CreatedAt == null || file.CreatedAt > cutoff)
                {
                    report.Skipped++;
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!dryRun)
                    await rawStorage.DeleteAsync(file.Path);

                report.Processed++;
                report.BytesBefore += file.Size;
                report.Items.Add(new ImageMaintenanceItem { Name = "Huérfano", OldPath = file.Path, BytesBefore = file.Size });
            }

            logger.LogInformation(
                "Limpieza de huérfanos en products/ (dryRun={DryRun}): {Count} archivos, {Bytes} bytes; {Skipped} recientes omitidos",
                dryRun, report.Processed, report.BytesBefore, report.Skipped);

            return report;
        }

        public async Task<ImageMaintenanceReport> OptimizeProductImagesAsync(bool dryRun, bool deleteOriginals, CancellationToken cancellationToken = default)
        {
            var report = new ImageMaintenanceReport { DryRun = dryRun };
            var products = await db.Products
                .Where(p => p.ImageUrl != null && p.ImageUrl != "")
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            // Varios productos pueden compartir la misma imagen: se procesa una sola vez.
            var converted = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var product in products)
            {
                var oldPath = product.ImageUrl!;
                var item = new ImageMaintenanceItem { EntityId = product.Id, Name = product.Name, OldPath = oldPath };

                if (StorageUrlResolver.IsAbsoluteOrInline(oldPath) || oldPath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    report.Skipped++;
                    continue;
                }

                if (converted.TryGetValue(oldPath, out var alreadyConverted))
                {
                    if (!dryRun) product.ImageUrl = alreadyConverted;
                    item.NewPath = alreadyConverted;
                    report.Processed++;
                    report.Items.Add(item);
                    continue;
                }

                try
                {
                    await using var original = await rawStorage.DownloadAsync(oldPath);
                    if (original == null)
                    {
                        item.Error = "No existe en el storage.";
                        report.Failed++;
                        report.Items.Add(item);
                        continue;
                    }

                    item.BytesBefore = original.Length;
                    using var optimized = await optimizer.OptimizeAsync(original, ImageProfile.Catalog, cancellationToken);
                    item.BytesAfter = optimized.Length;

                    if (!dryRun)
                    {
                        var newPath = await rawStorage.UploadAsync(optimized.Content, $"image{optimized.Extension}", optimized.ContentType, "products");
                        product.ImageUrl = newPath;
                        product.UpdatedAt = DateTimeOffset.UtcNow;
                        await db.SaveChangesAsync(cancellationToken);

                        converted[oldPath] = newPath;
                        item.NewPath = newPath;

                        if (deleteOriginals)
                            await rawStorage.DeleteAsync(oldPath);
                    }

                    report.Processed++;
                    report.BytesBefore += item.BytesBefore;
                    report.BytesAfter += item.BytesAfter;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "No se pudo optimizar la imagen del producto {ProductId} ({Path})", product.Id, oldPath);
                    item.Error = ex.Message;
                    report.Failed++;
                }

                report.Items.Add(item);
            }

            if (!dryRun)
                await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Optimización de imágenes de producto (dryRun={DryRun}): {Processed} procesadas, {Failed} con error, {Before} → {After} bytes",
                dryRun, report.Processed, report.Failed, report.BytesBefore, report.BytesAfter);

            return report;
        }

        public async Task<ImageMaintenanceReport> MovePrivateFilesAsync(bool dryRun, CancellationToken cancellationToken = default)
        {
            var report = new ImageMaintenanceReport { DryRun = dryRun };

            var payments = await db.Payments
                .Where(p => p.EvidenceUrl != null && p.EvidenceUrl != "")
                .ToListAsync(cancellationToken);
            foreach (var payment in payments.Where(p => IsPublicStoragePath(p.EvidenceUrl)))
            {
                var newPath = await MoveToPrivateAsync(report, payment.Id, $"Comprobante {payment.OrderId}", payment.EvidenceUrl!,
                    $"payment-evidence/{payment.OrderId}", ImageProfile.Document, dryRun, cancellationToken);
                if (newPath != null)
                {
                    payment.EvidenceUrl = newPath;
                    await SaveAndDeleteAsync(report, newPath, cancellationToken);
                }
            }

            var orders = await db.Orders
                .Where(o => (o.ShippingProofUrl != null && o.ShippingProofUrl != "") ||
                            (o.ReferenceImageUrl != null && !o.ReferenceImageUrl.StartsWith("data:")))
                .ToListAsync(cancellationToken);
            foreach (var order in orders)
            {
                if (IsPublicStoragePath(order.ShippingProofUrl))
                {
                    var newPath = await MoveToPrivateAsync(report, order.Id, $"Envío {order.OrderCode}", order.ShippingProofUrl!,
                        "shipping-evidence", ImageProfile.Document, dryRun, cancellationToken);
                    if (newPath != null)
                    {
                        order.ShippingProofUrl = newPath;
                        await SaveAndDeleteAsync(report, newPath, cancellationToken);
                    }
                }

                if (IsPublicStoragePath(order.ReferenceImageUrl))
                {
                    var newPath = await MoveToPrivateAsync(report, order.Id, $"Referencia {order.OrderCode}", order.ReferenceImageUrl!,
                        ReferenceImageService.Folder, ImageProfile.Catalog, dryRun, cancellationToken);
                    if (newPath != null)
                    {
                        order.ReferenceImageUrl = newPath;
                        await SaveAndDeleteAsync(report, newPath, cancellationToken);
                    }
                }
            }

            return report;
        }

        private static bool IsPublicStoragePath(string? stored) =>
            !string.IsNullOrWhiteSpace(stored) &&
            !stored.StartsWith(SupabaseStorageService.PrivatePrefix, StringComparison.OrdinalIgnoreCase) &&
            !StorageUrlResolver.IsAbsoluteOrInline(stored);

        /// <summary>
        /// Copia el archivo al bucket privado (optimizándolo si es imagen) y devuelve la nueva ruta.
        /// La copia pública se borra en <see cref="SaveAndDeleteAsync"/> solo después de guardar la BD.
        /// </summary>
        private async Task<string?> MoveToPrivateAsync(ImageMaintenanceReport report, Guid entityId, string name, string oldPath,
            string folder, ImageProfile profile, bool dryRun, CancellationToken cancellationToken)
        {
            var item = new ImageMaintenanceItem { EntityId = entityId, Name = name, OldPath = oldPath };
            report.Items.Add(item);

            if (dryRun)
            {
                report.Processed++;
                return null;
            }

            try
            {
                await using var original = await rawStorage.DownloadAsync(oldPath);
                if (original == null)
                {
                    item.Error = "No existe en el storage.";
                    report.Failed++;
                    return null;
                }

                item.BytesBefore = original.Length;
                string newPath;
                try
                {
                    using var optimized = await optimizer.OptimizeAsync(original, profile, cancellationToken);
                    item.BytesAfter = optimized.Length;
                    newPath = await rawStorage.UploadAsync(optimized.Content, $"file{optimized.Extension}", optimized.ContentType, folder);
                }
                catch (InvalidImageException)
                {
                    // No es imagen (p. ej. PDF de la agencia): se mueve sin cambios
                    original.Position = 0;
                    item.BytesAfter = original.Length;
                    var extension = Path.GetExtension(oldPath).ToLowerInvariant();
                    var contentType = extension == ".pdf" ? "application/pdf" : "application/octet-stream";
                    newPath = await rawStorage.UploadAsync(original, $"file{extension}", contentType, folder);
                }

                item.NewPath = newPath;
                report.Processed++;
                report.BytesBefore += item.BytesBefore;
                report.BytesAfter += item.BytesAfter;
                return newPath;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "No se pudo mover {Path} al bucket privado", oldPath);
                item.Error = ex.Message;
                report.Failed++;
                return null;
            }
        }

        private async Task SaveAndDeleteAsync(ImageMaintenanceReport report, string newPath, CancellationToken cancellationToken)
        {
            await db.SaveChangesAsync(cancellationToken);
            var item = report.Items.Last(i => i.NewPath == newPath);
            await rawStorage.DeleteAsync(item.OldPath!);
        }

        public async Task<ImageMaintenanceReport> MigrateReferenceImagesAsync(bool dryRun, CancellationToken cancellationToken = default)
        {
            var report = new ImageMaintenanceReport { DryRun = dryRun };

            var orderIds = await db.Orders
                .Where(o => o.ReferenceImageUrl != null && o.ReferenceImageUrl.StartsWith("data:"))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            // Uno por uno: cada fila puede pesar varios MB.
            foreach (var orderId in orderIds)
            {
                var order = await db.Orders.FirstAsync(o => o.Id == orderId, cancellationToken);
                var item = new ImageMaintenanceItem
                {
                    EntityId = order.Id,
                    Name = order.OrderCode,
                    BytesBefore = order.ReferenceImageUrl!.Length
                };

                try
                {
                    if (!dryRun)
                    {
                        var path = await referenceImages.PersistAsync(order.ReferenceImageUrl, cancellationToken);
                        order.ReferenceImageUrl = path;
                        order.UpdatedAt = DateTimeOffset.UtcNow;
                        await db.SaveChangesAsync(cancellationToken);
                        item.NewPath = path;
                        item.BytesAfter = path?.Length ?? 0;
                    }

                    report.Processed++;
                    report.BytesBefore += item.BytesBefore;
                    report.BytesAfter += item.BytesAfter;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "No se pudo migrar la imagen de referencia del pedido {OrderCode}", order.OrderCode);
                    item.Error = ex.Message;
                    report.Failed++;
                }

                report.Items.Add(item);
                db.Entry(order).State = EntityState.Detached;
            }

            return report;
        }
    }
}
