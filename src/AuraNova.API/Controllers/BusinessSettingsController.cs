using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using AuraNova.API.Configuration;

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

        /// <summary>Paleta de colores (con su código para dibujar la muestra) y tipos de flor.</summary>
        [HttpGet("/api/customization-options")]
        [OutputCache(PolicyName = CatalogCache.Policy)]
        public async Task<IActionResult> GetCustomizationOptions()
        {
            return Ok(await _service.GetCustomizationOptionsAsync());
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
                }
            });
        }
    }
}
