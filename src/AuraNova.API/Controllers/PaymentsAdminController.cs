using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.Payments.DTOs;
using AuraNova.Application.Payments.Interfaces;
using AuraNova.Infrastructure.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/payments")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class PaymentsAdminController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsAdminController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _paymentService.GetAdminPaymentsAsync());
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var payment = await _paymentService.GetAdminPaymentByIdAsync(id);
            if (payment == null)
                return NotFound(new { message = "Pago no encontrado." });

            return Ok(payment);
        }

        [HttpPatch("{id:guid}/confirm")]
        public async Task<IActionResult> Confirm(Guid id)
        {
            try
            {
                var success = await _paymentService.ConfirmAsync(id);
                if (!success)
                    return NotFound(new { message = "Pago no encontrado." });

                return Ok(new { message = "Pago confirmado exitosamente." });
            }
            catch (OrderValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPatch("{id:guid}/reject")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectPaymentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var success = await _paymentService.RejectAsync(id, request);
                if (!success)
                    return NotFound(new { message = "Pago no encontrado." });

                return Ok(new { message = "Pago rechazado. El pedido volvió a esperar pago." });
            }
            catch (OrderValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
