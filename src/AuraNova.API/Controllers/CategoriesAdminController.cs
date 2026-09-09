using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.Categories.DTOs;
using AuraNova.Application.Categories.Interfaces;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.API.Extensions;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/categories")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class CategoriesAdminController(
        ICategoryService categoryService,
        IAdminAuditService auditService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await categoryService.GetAllAsync(includeInactive: true);
            return Ok(categories);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var category = await categoryService.GetByIdAsync(id);
            if (category == null)
                return NotFound();

            return Ok(category);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await categoryService.CreateAsync(request);
            if (category == null)
            {
                return Conflict(new { message = "Slug de categoría ya existe." });
            }
            
            await this.LogActionAsync(auditService, "Created", "Category", category.Id.ToString(), $"Categoría '{category.Name}' creada.");
            
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await categoryService.UpdateAsync(id, request);
            if (category == null)
            {
                return Conflict(new { message = "La categoría no existe o el slug ya está en uso." });
            }

            await this.LogActionAsync(auditService, "Updated", "Category", id.ToString(), $"Categoría '{category.Name}' editada.");

            return Ok(category);
        }

        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateCategoryStatusRequest request)
        {
            var result = await categoryService.UpdateStatusAsync(id, request.IsActive);
            if (!result)
                return NotFound();

            await this.LogActionAsync(auditService, "UpdateStatus", "Category", id.ToString(), $"Estado cambiado a {(request.IsActive ? "Activo" : "Inactivo")}.");

            return Ok(new { message = "Estado actualizado correctamente." });
        }
    }
}
