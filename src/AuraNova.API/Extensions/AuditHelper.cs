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

            // ForwardedHeaders (Program.cs) already resolved the real client IP behind Render's proxy;
            // the first X-Forwarded-For entry can be forged by the client.
            var ipAddress = controller.HttpContext.Connection.RemoteIpAddress?.ToString();

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
