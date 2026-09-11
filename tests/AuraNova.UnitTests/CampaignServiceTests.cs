using System;
using System.Threading.Tasks;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Application.Campaigns.DTOs;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Campaigns;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class CampaignServiceTests
    {
        private AppDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateCampaignAsync_ValidData_CreatesCampaign()
        {
            using var db = GetInMemoryDb();
            var mockAudit = new Mock<IAdminAuditService>();
            var mockAuth = new Mock<ICurrentUserService>();
            mockAuth.Setup(a => a.UserId).Returns(Guid.NewGuid());

            var service = new CampaignService(db, mockAudit.Object, mockAuth.Object, new NullLogger<CampaignService>());

            var request = new CreateCampaignRequest
            {
                Name = "Test Campaign",
                StartDate = DateTimeOffset.UtcNow,
                EndDate = DateTimeOffset.UtcNow.AddDays(10),
                IsActive = true
            };

            var result = await service.CreateCampaignAsync(request);

            Assert.NotNull(result);
            Assert.Equal("Test Campaign", result.Name);
            mockAudit.Verify(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<AuraNova.Application.Audit.DTOs.AdminAuditEntry>()), Times.Once);
        }

        [Fact]
        public async Task CreateCampaignAsync_InvalidDates_ThrowsException()
        {
            using var db = GetInMemoryDb();
            var mockAudit = new Mock<IAdminAuditService>();
            var mockAuth = new Mock<ICurrentUserService>();

            var service = new CampaignService(db, mockAudit.Object, mockAuth.Object, new NullLogger<CampaignService>());

            var request = new CreateCampaignRequest
            {
                Name = "Test Campaign",
                StartDate = DateTimeOffset.UtcNow.AddDays(10),
                EndDate = DateTimeOffset.UtcNow, // End is before Start
                IsActive = true
            };

            await Assert.ThrowsAsync<Exception>(() => service.CreateCampaignAsync(request));
        }

        [Fact]
        public async Task AddCampaignStageAsync_ValidData_CreatesStage()
        {
            using var db = GetInMemoryDb();
            var campaign = new Campaign { Id = Guid.NewGuid(), Name = "C1", StartDate = DateTimeOffset.UtcNow.AddDays(-1), EndDate = DateTimeOffset.UtcNow.AddDays(10), IsActive = true };
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync();

            var mockAudit = new Mock<IAdminAuditService>();
            var mockAuth = new Mock<ICurrentUserService>();
            var service = new CampaignService(db, mockAudit.Object, mockAuth.Object, new NullLogger<CampaignService>());

            var request = new CreateCampaignStageRequest
            {
                Name = "Stage 1",
                StartDate = DateTimeOffset.UtcNow,
                EndDate = DateTimeOffset.UtcNow.AddDays(5)
            };

            var result = await service.AddCampaignStageAsync(campaign.Id, request);

            Assert.NotNull(result);
            Assert.Equal("Stage 1", result.Name);
            Assert.Equal(campaign.Id, result.CampaignId);
        }

        [Fact]
        public async Task AddCampaignStageAsync_DatesOutsideCampaign_ThrowsException()
        {
            using var db = GetInMemoryDb();
            var campaign = new Campaign { Id = Guid.NewGuid(), Name = "C1", StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(5), IsActive = true };
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync();

            var mockAudit = new Mock<IAdminAuditService>();
            var mockAuth = new Mock<ICurrentUserService>();
            var service = new CampaignService(db, mockAudit.Object, mockAuth.Object, new NullLogger<CampaignService>());

            var request = new CreateCampaignStageRequest
            {
                Name = "Stage 1",
                StartDate = DateTimeOffset.UtcNow.AddDays(6), // Outside campaign range
                EndDate = DateTimeOffset.UtcNow.AddDays(10)
            };

            await Assert.ThrowsAsync<Exception>(() => service.AddCampaignStageAsync(campaign.Id, request));
        }
    }
}
