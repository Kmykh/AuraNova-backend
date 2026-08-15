using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.AdminOrders.DTOs;
using AuraNova.Application.AdminOrders.Interfaces;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/orders")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class OrdersAdminController : ControllerBase
    {
        private readonly IOrderStatusService _statusService;
        private readonly IAdminOrderQueryService _queryService;

        public OrdersAdminController(IOrderStatusService statusService, IAdminOrderQueryService queryService)
        {
            _statusService = statusService;
            _queryService = queryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders([FromQuery] AdminOrderFilterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _queryService.GetOrdersAsync(request);
            return Ok(response);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOrderDetail(Guid id)
        {
            var response = await _queryService.GetOrderDetailAsync(id);
            if (response == null)
                return NotFound(new { message = $"Pedido '{id}' no encontrado." });

            return Ok(response);
        }

        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeOrderStatusRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var newStatus))
                return BadRequest(new { message = $"Estado inválido: '{request.Status}'." });

            try
            {
                var result = await _statusService.ChangeStatusAsync(id, newStatus, request.Comment);
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

        [HttpGet("{id:guid}/status-history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            try
            {
                var history = await _statusService.GetHistoryAsync(id);
                return Ok(history);
            }
            catch (OrderNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
