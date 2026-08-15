using System;
using System.Threading.Tasks;
using AuraNova.Application.Payments.Interfaces;
using AuraNova.Infrastructure.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IO;
using System.Linq;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly AuraNova.Application.BusinessSettings.Interfaces.IBusinessSettingsService _businessSettingsService;

        public PaymentsController(IPaymentService paymentService, AuraNova.Application.BusinessSettings.Interfaces.IBusinessSettingsService businessSettingsService)
        {
            _paymentService = paymentService;
            _businessSettingsService = businessSettingsService;
        }

        [HttpGet("payment-info")]
        public async Task<IActionResult> GetPaymentInfo()
        {
            var settings = await _businessSettingsService.GetPublicAsync();
            return Ok(new
            {
                enabled = true,
                method = "Yape",
                holderName = settings.YapeHolderName,
                qrImageUrl = settings.YapeQrImageUrl,
                businessName = settings.BusinessName
            });
        }

        [HttpPost("orders/{orderId:guid}/payment-evidence")]
        [Consumes("multipart/form-data")]
        [EnableRateLimiting("evidence_upload_policy")]
        public async Task<IActionResult> ReportEvidence(Guid orderId, IFormFile evidence)
        {
            if (evidence == null || evidence.Length == 0)
                return BadRequest(new { message = "Se requiere un archivo de evidencia válido." });

            if (evidence.Length > 5 * 1024 * 1024) // 5 MB max
                return BadRequest(new { message = "El archivo no debe exceder los 5 MB." });

            var ext = Path.GetExtension(evidence.FileName).ToLowerInvariant();
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Tipo de archivo no permitido. Solo jpg, png y webp." });

            // Generar nombre seguro aleatorio
            var safeFileName = $"{Guid.NewGuid()}{ext}";

            try
            {
                using var stream = evidence.OpenReadStream();
                var result = await _paymentService.ReportEvidenceAsync(orderId, stream, safeFileName, evidence.ContentType);
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
