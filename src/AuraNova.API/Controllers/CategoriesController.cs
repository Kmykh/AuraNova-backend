using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.Categories.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/categories")]
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
