using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.Products.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _productService.GetPublicProductsAsync();
            return Ok(products);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _productService.GetPublicByIdAsync(id);
            if (product == null)
                return NotFound();

            return Ok(product);
        }
    }
}
