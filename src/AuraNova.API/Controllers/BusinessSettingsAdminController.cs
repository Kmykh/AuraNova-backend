using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.Application.BusinessSettings.DTOs;
using AuraNova.Application.BusinessSettings.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/admin/business-settings")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin_policy")]
    public class BusinessSettingsAdminController : ControllerBase
    {
        private readonly IBusinessSettingsService _service;

        public BusinessSettingsAdminController(IBusinessSettingsService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var result = await _service.GetAdminAsync();
            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateBusinessSettingsRequest request)
        {
            try
            {
                var result = await _service.UpdateAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("yape-qr")]
        public async Task<IActionResult> UploadQr(IFormFile qr)
        {
            if (qr == null || qr.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            if (qr.Length > 5 * 1024 * 1024)
                return BadRequest(new { error = "File size exceeds 5MB limit." });

            var ext = Path.GetExtension(qr.FileName).ToLowerInvariant();
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { error = "Invalid file type. Only jpg, png, webp allowed." });

            var safeFileName = $"{Guid.NewGuid()}{ext}";

            try
            {
                using var stream = qr.OpenReadStream();
                var result = await _service.UploadYapeQrAsync(stream, safeFileName, qr.ContentType);
                return Ok(new { qrImageUrl = result.YapeQrImageUrl });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("yape-qr")]
        public async Task<IActionResult> RemoveQr()
        {
            await _service.RemoveYapeQrAsync();
            return NoContent();
        }
    }
}
