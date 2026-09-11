using System;
using System.Threading.Tasks;
using AuraNova.Application.Campaigns.DTOs;
using AuraNova.Application.Campaigns.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ApiController]
    [Route("api/admin/campaigns")]
    public class CampaignsAdminController : ControllerBase
    {
        private readonly ICampaignService _campaignService;

        public CampaignsAdminController(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var campaigns = await _campaignService.GetAllCampaignsAsync();
            return Ok(campaigns);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var campaign = await _campaignService.GetCampaignByIdAsync(id);
            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
        {
            try
            {
                var campaign = await _campaignService.CreateCampaignAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = campaign.Id }, campaign);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCampaignRequest request)
        {
            try
            {
                var campaign = await _campaignService.UpdateCampaignAsync(id, request);
                if (campaign == null)
                    return NotFound();

                return Ok(campaign);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id, [FromBody] bool isActive)
        {
            var result = await _campaignService.ToggleCampaignStatusAsync(id, isActive);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _campaignService.DeleteCampaignAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        // STAGES
        [HttpPost("{id}/stages")]
        public async Task<IActionResult> AddStage(Guid id, [FromBody] CreateCampaignStageRequest request)
        {
            try
            {
                var stage = await _campaignService.AddCampaignStageAsync(id, request);
                return Ok(stage);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPut("{id}/stages/{stageId}")]
        public async Task<IActionResult> UpdateStage(Guid id, Guid stageId, [FromBody] UpdateCampaignStageRequest request)
        {
            try
            {
                var stage = await _campaignService.UpdateCampaignStageAsync(id, stageId, request);
                if (stage == null)
                    return NotFound();

                return Ok(stage);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpDelete("{id}/stages/{stageId}")]
        public async Task<IActionResult> DeleteStage(Guid id, Guid stageId)
        {
            var result = await _campaignService.DeleteCampaignStageAsync(id, stageId);
            if (!result)
                return NotFound();

            return NoContent();
        }

        // PRODUCTS
        [HttpPost("{id}/products")]
        public async Task<IActionResult> AddProduct(Guid id, [FromBody] AddCampaignProductRequest request)
        {
            try
            {
                var product = await _campaignService.AddProductToCampaignAsync(id, request);
                return Ok(product);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpDelete("{id}/products/{productId}")]
        public async Task<IActionResult> RemoveProduct(Guid id, Guid productId)
        {
            var result = await _campaignService.RemoveProductFromCampaignAsync(id, productId);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/products/{productId}/stages/{stageId}/price")]
        public async Task<IActionResult> SetProductStagePrice(Guid id, Guid productId, Guid stageId, [FromBody] SetCampaignProductPriceRequest request)
        {
            try
            {
                var price = await _campaignService.SetProductStagePriceAsync(id, productId, stageId, request);
                return Ok(price);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
