using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AuraNova.Application.Audit.DTOs;
using AuraNova.Application.Audit.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuraNova.API.Extensions
{
    public static class AuditHelper
    {
        public static async Task LogActionAsync(
            this ControllerBase controller,
            IAdminAuditService auditService,
            string action,
            string entityType,
            string? entityId = null,
            string? description = null)
        {
            var adminIdStr = controller.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminIdStr) || !Guid.TryParse(adminIdStr, out var adminUserId))
            {
                // Should not happen since endpoints have [Authorize(Roles = "Admin")]
                return;
            }

            var ipAddress = controller.HttpContext.Connection.RemoteIpAddress?.ToString();
            // Handle forwarded headers from proxy/Render if available
            if (controller.HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                ipAddress = forwardedFor.ToString().Split(',')[0].Trim();
            }

            var userAgent = controller.HttpContext.Request.Headers["User-Agent"].ToString();

            var entry = new AdminAuditEntry
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            await auditService.LogAsync(adminUserId, entry);
        }
    }
}
