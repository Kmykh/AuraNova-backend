namespace AuraNova.Application.Storage.Interfaces
{
    public interface IImageMaintenanceService
    {
        /// <summary>
        /// Re-procesa las imágenes de producto que aún no están en WebP optimizado y actualiza Products.ImageUrl.
        /// Con dryRun = true solo calcula el ahorro estimado.
        /// </summary>
        Task<ImageMaintenanceReport> OptimizeProductImagesAsync(bool dryRun, bool deleteOriginals, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mueve las imágenes de referencia guardadas como base64 en Orders.ReferenceImageUrl al bucket.
        /// </summary>
        Task<ImageMaintenanceReport> MigrateReferenceImagesAsync(bool dryRun, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mueve al bucket privado los archivos de clientes que quedaron en el bucket público
        /// (comprobantes de pago, constancias de envío e imágenes de referencia) y borra la copia pública.
        /// </summary>
        Task<ImageMaintenanceReport> MovePrivateFilesAsync(bool dryRun, CancellationToken cancellationToken = default);

        /// <summary>
        /// Borra de products/ los archivos que ningún producto usa (imágenes reemplazadas, originales ya convertidos).
        /// No toca archivos subidos en las últimas <paramref name="minAgeHours"/> horas: pueden ser de un producto
        /// que se está editando y aún no se guardó.
        /// </summary>
        Task<ImageMaintenanceReport> CleanupOrphanProductImagesAsync(bool dryRun, int minAgeHours = 24, CancellationToken cancellationToken = default);
    }

    public class ImageMaintenanceReport
    {
        public bool DryRun { get; set; }
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public long BytesBefore { get; set; }
        public long BytesAfter { get; set; }
        public List<ImageMaintenanceItem> Items { get; set; } = [];
    }

    public class ImageMaintenanceItem
    {
        public Guid EntityId { get; set; }
        public string? Name { get; set; }
        public string? OldPath { get; set; }
        public string? NewPath { get; set; }
        public long BytesBefore { get; set; }
        public long BytesAfter { get; set; }
        public string? Error { get; set; }
    }
}
