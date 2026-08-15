using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.Products.DTOs;
using AuraNova.Application.Products.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/products")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class ProductsAdminController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsAdminController(IProductService productService)
        {
            _productService = productService;
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
    }
}
