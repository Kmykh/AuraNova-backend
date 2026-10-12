using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using AuraNova.Application.Orders.DTOs;
using AuraNova.Application.Orders.Interfaces;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.API.Configuration;
using AuraNova.Infrastructure.Orders;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private const int MaxReferenceImages = 5;
        private const long MaxReferenceImageBytes = 10 * 1024 * 1024;
        private const long MaxCustomOrderUploadBytes = 55 * 1024 * 1024;

        private static readonly JsonSerializerOptions FormJsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly IOrderService _orderService;
        private readonly IReferenceImageService _referenceImages;
        private readonly IFileStorageService _storage;

        public OrdersController(IOrderService orderService, IReferenceImageService referenceImages, IFileStorageService storage)
        {
            _orderService = orderService;
            _referenceImages = referenceImages;
            _storage = storage;
        }

        /// <summary>
        /// Creates a new order. Public endpoint — no authentication required.
        /// The backend validates products, availability, stock, and calculates prices from DB.
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("create_order_policy")]
        [EvictCatalogCache] // el pedido descuenta stock, que se muestra en el catálogo
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

        /// <summary>
        /// Creates a new custom order (100% personalized).
        /// </summary>
        [HttpPost("custom")]
        [Consumes("application/json")]
        [EnableRateLimiting("create_order_policy")]
        public async Task<IActionResult> CreateCustom([FromBody] CreateCustomOrderRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // El frontend envía la imagen como data URL base64: se guarda en el storage y en BD solo la ruta.
                request.ReferenceImageUrl = await _referenceImages.PersistAsync(request.ReferenceImageUrl, HttpContext.RequestAborted);

                var result = await _orderService.CreateCustomAsync(request);
                return StatusCode(201, result);
            }
            catch (InvalidImageException ex)
            {
                return BadRequest(new { message = ex.Message });
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

        /// <summary>
        /// Pedido personalizado con hasta 5 imágenes de referencia como archivos (multipart/form-data):
        /// campo "data" con el JSON del pedido y campo "images" con los archivos.
        /// Las imágenes se validan, se optimizan a WebP sin metadatos y se guardan en el bucket privado.
        /// </summary>
        [HttpPost("custom")]
        [Consumes("multipart/form-data")]
        [EnableRateLimiting("create_order_policy")]
        [RequestSizeLimit(MaxCustomOrderUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxCustomOrderUploadBytes)]
        public async Task<IActionResult> CreateCustomWithImages([FromForm] string data, [FromForm] List<IFormFile>? images)
        {
            CreateCustomOrderRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<CreateCustomOrderRequest>(data, FormJsonOptions);
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Los datos del pedido no tienen un formato válido." });
            }

            if (request == null || !TryValidateModel(request))
                return ValidationProblem(ModelState);

            images ??= [];
            if (images.Count > MaxReferenceImages)
                return BadRequest(new { message = $"Puedes enviar hasta {MaxReferenceImages} imágenes de referencia." });

            if (images.Any(i => i.Length == 0 || i.Length > MaxReferenceImageBytes))
                return BadRequest(new { message = "Cada imagen debe pesar menos de 10 MB." });

            var uploaded = new List<string>();
            try
            {
                foreach (var image in images)
                {
                    await using var stream = image.OpenReadStream();
                    uploaded.Add(await _referenceImages.PersistFileAsync(stream, image.ContentType, HttpContext.RequestAborted));
                }

                // Never trust image paths from the client: only the ones uploaded right here
                request.ReferenceImageUrl = null;
                var result = await _orderService.CreateCustomAsync(request, uploaded);
                return StatusCode(201, result);
            }
            catch (Exception ex) when (ex is InvalidImageException or OrderValidationException or OrderNotFoundException)
            {
                // The order was not created: remove the images that were already stored
                foreach (var path in uploaded)
                    await _storage.DeleteAsync(path);

                return ex is OrderNotFoundException
                    ? NotFound(new { message = ex.Message })
                    : BadRequest(new { message = ex.Message });
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
