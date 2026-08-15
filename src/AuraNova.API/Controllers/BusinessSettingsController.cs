using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/business-settings")]
    public class BusinessSettingsController : ControllerBase
    {
        private readonly IBusinessSettingsService _service;

        public BusinessSettingsController(IBusinessSettingsService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetPublicSettings()
        {
            var settings = await _service.GetPublicAsync();
            
            // Return only public information
            return Ok(new
            {
                businessName = settings.BusinessName,
                whatsappNumber = settings.WhatsAppNumber,
                yape = new
                {
                    holderName = settings.YapeHolderName,
                    qrImageUrl = settings.YapeQrImageUrl
                },
                trackingBaseUrl = settings.TrackingBaseUrl
            });
        }
    }
}
