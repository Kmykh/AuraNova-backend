using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using AuraNova.API.Configuration;
using AuraNova.Application.Categories.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/categories")]
    [OutputCache(PolicyName = CatalogCache.Policy)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public class CategoriesController(ICategoryService categoryService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await categoryService.GetAllAsync(includeInactive: false);
            return Ok(categories);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var category = await categoryService.GetByIdAsync(id);
            if (category == null || !category.IsActive)
                return NotFound();

            return Ok(category);
        }
    }
}
