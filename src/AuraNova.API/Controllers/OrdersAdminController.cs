using AuraNova.API.Extensions;
using AuraNova.Application.AdminOrders.DTOs;
using AuraNova.Application.AdminOrders.Interfaces;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/orders")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class OrdersAdminController(
        IOrderStatusService statusService,
        IAdminOrderQueryService queryService,
        IAdminAuditService auditService,
        IOrderService orderService,
        IFileStorageService storageService) : ControllerBase
    {
        private readonly IOrderStatusService _statusService = statusService;
        private readonly IAdminOrderQueryService _queryService = queryService;
        private readonly IAdminAuditService _auditService = auditService;
        private readonly IOrderService _orderService = orderService;
        private readonly IFileStorageService _storageService = storageService;


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

                await this.LogActionAsync(_auditService, "UpdateStatus", "Order", id.ToString(),
                    $"Estado de pedido cambiado a '{newStatus}'.");

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

        [HttpPost("{id:guid}/start-preparation")]
        public async Task<IActionResult> StartPreparation(Guid id)
        {
            try
            {
                await _orderService.StartPreparationAsync(id);
                await this.LogActionAsync(_auditService, "StartPreparation", "Order", id.ToString(),
                    "Elaboración iniciada.");
                return Ok(new { message = "Elaboración iniciada correctamente." });
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

        [HttpPatch("{id:guid}/estimated-date")]
        public async Task<IActionResult> SetEstimatedDate(Guid id, [FromBody] SetEstimatedReadyDateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _orderService.SetEstimatedReadyDateAsync(id, request.EstimatedDate);
                await this.LogActionAsync(_auditService, "SetEstimatedDate", "Order", id.ToString(),
                    $"Fecha estimada fijada: {request.EstimatedDate}");
                return Ok(new { message = "Fecha estimada actualizada." });
            }
            catch (OrderNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/mark-ready")]
        public async Task<IActionResult> MarkReady(Guid id)
        {
            try
            {
                await _orderService.MarkAsReadyAsync(id);
                await this.LogActionAsync(_auditService, "MarkReady", "Order", id.ToString(), "Pedido marcado como listo.");
                return Ok(new { message = "Pedido marcado como listo." });
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

        [HttpPost("{id:guid}/deliver-to-agency")]
        public async Task<IActionResult> DeliverToAgency(Guid id, [FromForm] string provider, [FromForm] string trackingCode, IFormFile? proofFile)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return BadRequest(new { message = "El proveedor de envío es obligatorio." });

            try
            {
                string? proofUrl = null;
                if (proofFile != null && proofFile.Length > 0)
                {
                    // Upload file using Storage Service
                    using var stream = proofFile.OpenReadStream();
                    proofUrl = await _storageService.UploadAsync(stream, proofFile.FileName, proofFile.ContentType, "shipping-evidence");
                }

                await _orderService.DeliverToAgencyAsync(id, provider, trackingCode, proofUrl);
                await this.LogActionAsync(_auditService, "DeliverToAgency", "Order", id.ToString(), $"Entregado a agencia {provider}.");
                return Ok(new { message = "Entrega a agencia registrada." });
            }
            catch (OrderNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (OrderValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al procesar la entrega a agencia.", details = ex.Message });
            }
        }
    }
}
