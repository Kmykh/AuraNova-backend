using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using AuraNova.Application.Products.DTOs;
using AuraNova.Application.Products.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.API.Extensions;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/products")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class ProductsAdminController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IAdminAuditService _auditService;

        public ProductsAdminController(
            IProductService productService, 
            IFileStorageService fileStorageService,
            IAdminAuditService auditService)
        {
            _productService = productService;
            _fileStorageService = fileStorageService;
            _auditService = auditService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = await _productService.CreateAsync(request);
            
            await this.LogActionAsync(_auditService, "Created", "Product", product.Id.ToString(), $"Producto '{product.Name}' creado.");
            
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _productService.GetAdminProductsAsync();
            return Ok(products);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _productService.GetAdminByIdAsync(id);
            if (product == null)
                return NotFound();

            return Ok(product);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = await _productService.UpdateAsync(id, request);
            if (product == null)
                return NotFound();

            await this.LogActionAsync(_auditService, "Updated", "Product", id.ToString(), $"Producto '{product.Name}' editado.");

            return Ok(product);
        }

        [HttpPatch("{id:guid}/stock")]
        public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateProductStockRequest request)
        {
            if (request.Stock < 0)
                return BadRequest(new { message = "Stock no puede ser negativo." });

            var result = await _productService.UpdateStockAsync(id, request.Stock);
            if (!result)
                return NotFound();

            await this.LogActionAsync(_auditService, "UpdateStock", "Product", id.ToString(), $"Stock actualizado a {request.Stock}.");

            return Ok(new { message = "Stock actualizado correctamente." });
        }

        [HttpPatch("{id:guid}/availability")]
        public async Task<IActionResult> UpdateAvailability(Guid id, [FromBody] UpdateProductAvailabilityRequest request)
        {
            var result = await _productService.UpdateAvailabilityAsync(id, request.IsAvailable);
            if (!result)
                return NotFound();

            await this.LogActionAsync(_auditService, "UpdateAvailability", "Product", id.ToString(), $"Disponibilidad cambiada a {(request.IsAvailable ? "Activo" : "Inactivo")}.");

            return Ok(new { message = "Disponibilidad actualizada correctamente." });
        }

        [HttpPost("upload-image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No se ha proporcionado ningún archivo." });

            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "El archivo no debe exceder los 5 MB." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg"; // fallback

            var safeFileName = $"{Guid.NewGuid()}{ext}";

            try
            {
                using var stream = file.OpenReadStream();
                // Subir a la carpeta "products" en el bucket
                var url = await _fileStorageService.UploadAsync(stream, safeFileName, file.ContentType, "products");
                
                await this.LogActionAsync(_auditService, "UploadImage", "Product", null, "Imagen subida para producto.");

                return Ok(new { imageUrl = url });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno al subir la imagen.", details = ex.ToString() });
            }
        }
    }
}
