using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Infrastructure.Orders;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Creates a new order. Public endpoint — no authentication required.
        /// The backend validates products, availability, stock, and calculates prices from DB.
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("create_order_policy")]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _orderService.CreateAsync(request);
                return StatusCode(201, result);
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
        [HttpPost("{id}/accept-quote")]
        [EnableRateLimiting("accept_quote_policy")]
        public async Task<IActionResult> AcceptQuote(Guid id)
        {
            try
            {
                var success = await _orderService.AcceptQuoteAsync(id);
                return Ok(new { success, message = "Cotización aceptada. Pedido listo para pago." });
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
