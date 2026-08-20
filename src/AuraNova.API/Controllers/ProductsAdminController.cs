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

        public ProductsAdminController(IProductService productService, IFileStorageService fileStorageService)
        {
            _productService = productService;
            _fileStorageService = fileStorageService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = await _productService.CreateAsync(request);
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

            return Ok(new { message = "Stock actualizado correctamente." });
        }

        [HttpPatch("{id:guid}/availability")]
        public async Task<IActionResult> UpdateAvailability(Guid id, [FromBody] UpdateProductAvailabilityRequest request)
        {
            var result = await _productService.UpdateAvailabilityAsync(id, request.IsAvailable);
            if (!result)
                return NotFound();

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
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Tipo de archivo no permitido. Solo jpg, png y webp." });

            var safeFileName = $"{Guid.NewGuid()}{ext}";

            try
            {
                using var stream = file.OpenReadStream();
                // Subir a la carpeta "products" en el bucket
                var url = await _fileStorageService.UploadAsync(stream, safeFileName, file.ContentType, "products");
                
                return Ok(new { imageUrl = url });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno al subir la imagen.", details = ex.Message });
            }
        }
    }
}
