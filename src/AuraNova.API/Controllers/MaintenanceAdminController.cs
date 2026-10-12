using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.API.Configuration;
using AuraNova.API.Extensions;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.Application.Storage.Interfaces;

namespace AuraNova.API.Controllers
{
    /// <summary>
    /// Tareas de saneamiento de imágenes. Ejecutar primero con dryRun=true (por defecto) para ver el ahorro.
    /// </summary>
    [ApiController]
    [Route("api/admin/maintenance/images")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class MaintenanceAdminController(
        IImageMaintenanceService maintenanceService,
        IAdminAuditService auditService) : ControllerBase
    {
        /// <summary>
        /// Convierte las imágenes de producto existentes a WebP optimizado (máx. 1600px) y actualiza los productos.
        /// Con deleteOriginals=true borra los originales una vez reemplazados.
        /// </summary>
        [HttpPost("optimize-products")]
        [EvictCatalogCache]
        public async Task<IActionResult> OptimizeProducts([FromQuery] bool dryRun = true, [FromQuery] bool deleteOriginals = false, CancellationToken cancellationToken = default)
        {
            var report = await maintenanceService.OptimizeProductImagesAsync(dryRun, deleteOriginals, cancellationToken);

            if (!dryRun)
                await this.LogActionAsync(auditService, "OptimizeProductImages", "Product", null,
                    $"Imágenes optimizadas: {report.Processed}. {report.BytesBefore} → {report.BytesAfter} bytes.");

            return Ok(report);
        }

        /// <summary>
        /// Mueve al bucket privado (customer-files) los comprobantes, constancias de envío e imágenes de referencia
        /// que quedaron en el bucket público, y borra la copia pública.
        /// </summary>
        [HttpPost("move-private-files")]
        public async Task<IActionResult> MovePrivateFiles([FromQuery] bool dryRun = true, CancellationToken cancellationToken = default)
        {
            var report = await maintenanceService.MovePrivateFilesAsync(dryRun, cancellationToken);

            if (!dryRun)
                await this.LogActionAsync(auditService, "MovePrivateFiles", "Storage", null,
                    $"Archivos de clientes movidos al bucket privado: {report.Processed}.");

            return Ok(report);
        }

        /// <summary>
        /// Mueve las imágenes de referencia de pedidos personalizados guardadas como base64 en la BD al storage.
        /// </summary>
        [HttpPost("migrate-reference-images")]
        public async Task<IActionResult> MigrateReferenceImages([FromQuery] bool dryRun = true, CancellationToken cancellationToken = default)
        {
            var report = await maintenanceService.MigrateReferenceImagesAsync(dryRun, cancellationToken);

            if (!dryRun)
                await this.LogActionAsync(auditService, "MigrateReferenceImages", "Order", null,
                    $"Imágenes de referencia migradas: {report.Processed}.");

            return Ok(report);
        }
    }
}
