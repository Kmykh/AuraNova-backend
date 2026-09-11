using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.Campaigns.DTOs;
using AuraNova.Application.Campaigns.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraNova.Infrastructure.Campaigns
{
    public class CampaignService(
        AppDbContext db,
        IAdminAuditService auditLogService,
        ICurrentUserService currentUserService,
        ILogger<CampaignService> logger) : ICampaignService
    {
        private readonly AppDbContext _db = db;
        private readonly IAdminAuditService _auditLogService = auditLogService;
        private readonly ICurrentUserService _currentUserService = currentUserService;
        private readonly ILogger<CampaignService> _logger = logger;

        public async Task<IReadOnlyList<CampaignResponse>> GetAllCampaignsAsync()
        {
            var campaigns = await _db.Campaigns
                .Include(c => c.Stages)
                .Include(c => c.Products)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return campaigns.Select(MapToResponse).ToList();
        }

        public async Task<CampaignResponse?> GetCampaignByIdAsync(Guid id)
        {
            var campaign = await _db.Campaigns
                .Include(c => c.Stages)
                .Include(c => c.Products!)
                    .ThenInclude(p => p.Product)
                .Include(c => c.Products!)
                    .ThenInclude(p => p.StagePrices)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campaign == null)
                return null;

            return MapToResponse(campaign);
        }

        public async Task<CampaignResponse> CreateCampaignAsync(CreateCampaignRequest request)
        {
            var adminId = _currentUserService.UserId ?? Guid.Empty;

            if (request.StartDate >= request.EndDate)
                throw new Exception("La fecha de inicio debe ser menor a la fecha de fin.");

            var campaign = new Campaign
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = request.IsActive
            };

            _db.Campaigns.Add(campaign);
            await _db.SaveChangesAsync();

            await _auditLogService.LogAsync(adminId, new AuraNova.Application.Audit.DTOs.AdminAuditEntry
            {
                Action = "Create",
                EntityType = "Campaign",
                EntityId = campaign.Id.ToString(),
                Description = "Creada nueva campaña"
            });

            _logger.LogInformation("Campaña creada: {CampaignId}", campaign.Id);

            return MapToResponse(campaign);
        }

        public async Task<CampaignResponse?> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request)
        {
            var adminId = _currentUserService.UserId ?? Guid.Empty;

            var campaign = await _db.Campaigns.FindAsync(id);
            if (campaign == null)
                return null;

            if (request.StartDate >= request.EndDate)
                throw new Exception("La fecha de inicio debe ser menor a la fecha de fin.");

            var oldValue = new { campaign.Name, campaign.StartDate, campaign.EndDate, campaign.IsActive };

            campaign.Name = request.Name.Trim();
            campaign.Description = request.Description?.Trim();
            campaign.StartDate = request.StartDate;
            campaign.EndDate = request.EndDate;
            campaign.IsActive = request.IsActive;
            campaign.UpdatedAt = DateTimeOffset.UtcNow;

            _db.Campaigns.Update(campaign);
            await _db.SaveChangesAsync();

            await _auditLogService.LogAsync(adminId, new AuraNova.Application.Audit.DTOs.AdminAuditEntry
            {
                Action = "Update",
                EntityType = "Campaign",
                EntityId = campaign.Id.ToString(),
                Description = "Actualizada la información de la campaña"
            });

            return MapToResponse(campaign);
        }

        public async Task<bool> ToggleCampaignStatusAsync(Guid id, bool isActive)
        {
            var adminId = _currentUserService.UserId ?? Guid.Empty;
            
            var campaign = await _db.Campaigns.FindAsync(id);
            if (campaign == null)
                return false;

            var oldStatus = campaign.IsActive;
            if (oldStatus == isActive)
                return true;

            campaign.IsActive = isActive;
            campaign.UpdatedAt = DateTimeOffset.UtcNow;
            
            _db.Campaigns.Update(campaign);
            await _db.SaveChangesAsync();

            await _auditLogService.LogAsync(adminId, new AuraNova.Application.Audit.DTOs.AdminAuditEntry
            {
                Action = "StatusChange",
                EntityType = "Campaign",
                EntityId = campaign.Id.ToString(),
                Description = isActive ? "Campaña activada" : "Campaña desactivada"
            });

            return true;
        }

        public async Task<bool> DeleteCampaignAsync(Guid id)
        {
            var adminId = _currentUserService.UserId ?? Guid.Empty;

            var campaign = await _db.Campaigns.FindAsync(id);
            if (campaign == null)
                return false;

            _db.Campaigns.Remove(campaign);
            await _db.SaveChangesAsync();

            await _auditLogService.LogAsync(adminId, new AuraNova.Application.Audit.DTOs.AdminAuditEntry
            {
                Action = "Delete",
                EntityType = "Campaign",
                EntityId = id.ToString(),
                Description = $"Eliminada la campaña {campaign.Name}"
            });

            return true;
        }

        public async Task<CampaignStageResponse> AddCampaignStageAsync(Guid campaignId, CreateCampaignStageRequest request)
        {
            var campaign = await _db.Campaigns.Include(c => c.Stages).FirstOrDefaultAsync(c => c.Id == campaignId);
            if (campaign == null)
                throw new Exception("Campaña no encontrada.");

            if (request.StartDate >= request.EndDate)
                throw new Exception("La fecha de inicio de la etapa debe ser menor a la fecha de fin.");

            if (request.StartDate < campaign.StartDate || request.EndDate > campaign.EndDate)
                throw new Exception("Las fechas de la etapa deben estar dentro del rango de la campaña padre.");

            var stage = new CampaignStage
            {
                CampaignId = campaignId,
                Name = request.Name.Trim(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = request.IsActive
            };

            _db.CampaignStages.Add(stage);
            await _db.SaveChangesAsync();

            return MapStageToResponse(stage);
        }

        public async Task<CampaignStageResponse?> UpdateCampaignStageAsync(Guid campaignId, Guid stageId, UpdateCampaignStageRequest request)
        {
            var stage = await _db.CampaignStages.Include(s => s.Campaign).FirstOrDefaultAsync(s => s.Id == stageId && s.CampaignId == campaignId);
            if (stage == null)
                return null;

            if (request.StartDate >= request.EndDate)
                throw new Exception("La fecha de inicio de la etapa debe ser menor a la fecha de fin.");

            if (request.StartDate < stage.Campaign!.StartDate || request.EndDate > stage.Campaign.EndDate)
                throw new Exception("Las fechas de la etapa deben estar dentro del rango de la campaña padre.");

            stage.Name = request.Name.Trim();
            stage.StartDate = request.StartDate;
            stage.EndDate = request.EndDate;
            stage.IsActive = request.IsActive;
            stage.UpdatedAt = DateTimeOffset.UtcNow;

            _db.CampaignStages.Update(stage);
            await _db.SaveChangesAsync();

            return MapStageToResponse(stage);
        }

        public async Task<bool> DeleteCampaignStageAsync(Guid campaignId, Guid stageId)
        {
            var stage = await _db.CampaignStages.FirstOrDefaultAsync(s => s.Id == stageId && s.CampaignId == campaignId);
            if (stage == null)
                return false;

            _db.CampaignStages.Remove(stage);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<CampaignProductResponse> AddProductToCampaignAsync(Guid campaignId, AddCampaignProductRequest request)
        {
            var campaign = await _db.Campaigns.FindAsync(campaignId);
            if (campaign == null)
                throw new Exception("Campaña no encontrada.");

            var product = await _db.Products.FindAsync(request.ProductId);
            if (product == null)
                throw new Exception("Producto no encontrado.");

            var existing = await _db.CampaignProducts.FirstOrDefaultAsync(cp => cp.CampaignId == campaignId && cp.ProductId == request.ProductId);
            if (existing != null)
                throw new Exception("El producto ya pertenece a esta campaña.");

            var campaignProduct = new CampaignProduct
            {
                CampaignId = campaignId,
                ProductId = request.ProductId,
                IsActive = request.IsActive
            };

            _db.CampaignProducts.Add(campaignProduct);
            await _db.SaveChangesAsync();

            // Fetch to get product name included
            var created = await _db.CampaignProducts
                .Include(cp => cp.Product)
                .Include(cp => cp.StagePrices)
                .FirstAsync(cp => cp.Id == campaignProduct.Id);

            return MapProductToResponse(created);
        }

        public async Task<bool> RemoveProductFromCampaignAsync(Guid campaignId, Guid productId)
        {
            var cp = await _db.CampaignProducts.FirstOrDefaultAsync(c => c.CampaignId == campaignId && c.ProductId == productId);
            if (cp == null)
                return false;

            _db.CampaignProducts.Remove(cp);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<CampaignProductStagePriceResponse> SetProductStagePriceAsync(Guid campaignId, Guid productId, Guid stageId, SetCampaignProductPriceRequest request)
        {
            var adminId = _currentUserService.UserId ?? Guid.Empty;

            var cp = await _db.CampaignProducts.FirstOrDefaultAsync(c => c.CampaignId == campaignId && c.ProductId == productId);
            if (cp == null)
                throw new Exception("El producto no está asignado a esta campaña.");

            var stage = await _db.CampaignStages.FirstOrDefaultAsync(s => s.Id == stageId && s.CampaignId == campaignId);
            if (stage == null)
                throw new Exception("La etapa no pertenece a esta campaña.");

            var stagePrice = await _db.CampaignProductStagePrices
                .FirstOrDefaultAsync(sp => sp.CampaignProductId == cp.Id && sp.CampaignStageId == stageId);

            string actionName = "";
            object? oldVal = null;
            if (stagePrice == null)
            {
                stagePrice = new CampaignProductStagePrice
                {
                    CampaignProductId = cp.Id,
                    CampaignStageId = stageId,
                    Price = request.Price
                };
                _db.CampaignProductStagePrices.Add(stagePrice);
                actionName = "CreatePrice";
            }
            else
            {
                oldVal = new { stagePrice.Price };
                stagePrice.Price = request.Price;
                stagePrice.UpdatedAt = DateTimeOffset.UtcNow;
                _db.CampaignProductStagePrices.Update(stagePrice);
                actionName = "UpdatePrice";
            }

            await _db.SaveChangesAsync();

            await _auditLogService.LogAsync(adminId, new AuraNova.Application.Audit.DTOs.AdminAuditEntry
            {
                Action = actionName,
                EntityType = "CampaignProductStagePrice",
                EntityId = stagePrice.Id.ToString(),
                Description = $"Precio fijado a {request.Price} para el producto en la etapa {stage.Name}"
            });

            return new CampaignProductStagePriceResponse
            {
                Id = stagePrice.Id,
                CampaignProductId = stagePrice.CampaignProductId,
                CampaignStageId = stagePrice.CampaignStageId,
                CampaignStageName = stage.Name,
                Price = stagePrice.Price,
                CreatedAt = stagePrice.CreatedAt
            };
        }

        private CampaignResponse MapToResponse(Campaign campaign)
        {
            return new CampaignResponse
            {
                Id = campaign.Id,
                Name = campaign.Name,
                Description = campaign.Description,
                StartDate = campaign.StartDate,
                EndDate = campaign.EndDate,
                IsActive = campaign.IsActive,
                CreatedAt = campaign.CreatedAt,
                Stages = campaign.Stages?.Select(MapStageToResponse).ToList() ?? new List<CampaignStageResponse>(),
                Products = campaign.Products?.Select(MapProductToResponse).ToList() ?? new List<CampaignProductResponse>()
            };
        }

        private CampaignStageResponse MapStageToResponse(CampaignStage stage)
        {
            return new CampaignStageResponse
            {
                Id = stage.Id,
                CampaignId = stage.CampaignId,
                Name = stage.Name,
                StartDate = stage.StartDate,
                EndDate = stage.EndDate,
                IsActive = stage.IsActive,
                CreatedAt = stage.CreatedAt
            };
        }

        private CampaignProductResponse MapProductToResponse(CampaignProduct cp)
        {
            return new CampaignProductResponse
            {
                Id = cp.Id,
                CampaignId = cp.CampaignId,
                ProductId = cp.ProductId,
                ProductName = cp.Product?.Name ?? "N/A",
                ProductImageUrl = cp.Product?.ImageUrl,
                ProductBasePrice = cp.Product?.Price ?? 0,
                IsActive = cp.IsActive,
                CreatedAt = cp.CreatedAt,
                StagePrices = cp.StagePrices?.Select(sp => new CampaignProductStagePriceResponse
                {
                    Id = sp.Id,
                    CampaignProductId = sp.CampaignProductId,
                    CampaignStageId = sp.CampaignStageId,
                    CampaignStageName = sp.CampaignStage?.Name ?? "Desconocido",
                    Price = sp.Price,
                    CreatedAt = sp.CreatedAt
                }).ToList() ?? new List<CampaignProductStagePriceResponse>()
            };
        }
    }
}
