using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuraNova.Application.Campaigns.DTOs;

namespace AuraNova.Application.Campaigns.Interfaces
{
    public interface ICampaignService
    {
        // Campaigns
        Task<IReadOnlyList<CampaignResponse>> GetAllCampaignsAsync();
        Task<CampaignResponse?> GetCampaignByIdAsync(Guid id);
        Task<CampaignResponse> CreateCampaignAsync(CreateCampaignRequest request);
        Task<CampaignResponse?> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request);
        Task<bool> ToggleCampaignStatusAsync(Guid id, bool isActive);
        Task<bool> DeleteCampaignAsync(Guid id);

        // Stages
        Task<CampaignStageResponse> AddCampaignStageAsync(Guid campaignId, CreateCampaignStageRequest request);
        Task<CampaignStageResponse?> UpdateCampaignStageAsync(Guid campaignId, Guid stageId, UpdateCampaignStageRequest request);
        Task<bool> DeleteCampaignStageAsync(Guid campaignId, Guid stageId);

        // Products
        Task<CampaignProductResponse> AddProductToCampaignAsync(Guid campaignId, AddCampaignProductRequest request);
        Task<bool> RemoveProductFromCampaignAsync(Guid campaignId, Guid productId);
        Task<CampaignProductStagePriceResponse> SetProductStagePriceAsync(Guid campaignId, Guid productId, Guid stageId, SetCampaignProductPriceRequest request);
    }
}
