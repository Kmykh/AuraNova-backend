using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.Quotes.DTOs;
using AuraNova.Application.Quotes.Interfaces;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.API.Extensions;
using AuraNova.Infrastructure.Orders;
using Microsoft.AspNetCore.RateLimiting;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/quotes")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class QuotesAdminController : ControllerBase
    {
        private readonly IQuoteService _quoteService;
        private readonly IAdminAuditService _auditService;

        public QuotesAdminController(IQuoteService quoteService, IAdminAuditService auditService)
        {
            _quoteService = quoteService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var quotes = await _quoteService.GetAllAsync();
            return Ok(quotes);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var quote = await _quoteService.GetByIdAsync(id);
            if (quote == null) return NotFound();
            return Ok(quote);
        }

        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuoteRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _quoteService.UpdateAsync(id, request);
                
                await this.LogActionAsync(_auditService, "UpdateQuote", "Quote", id.ToString(), $"Cotización respondida: S/{request.ShippingCost}.");
                
                return Ok(result);
            }
            catch (OrderNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (OrderValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
