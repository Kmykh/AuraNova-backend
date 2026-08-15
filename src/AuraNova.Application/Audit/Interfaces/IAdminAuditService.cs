using System;
using System.Threading.Tasks;
using AuraNova.Application.Audit.DTOs;
using AuraNova.Application.Common.Models;

namespace AuraNova.Application.Audit.Interfaces
{
    public interface IAdminAuditService
    {
        Task LogAsync(Guid adminUserId, AdminAuditEntry entry);
        Task<PagedResponse<AdminAuditLogResponse>> GetAuditLogsAsync(string? action = null, string? entityType = null, Guid? adminUserId = null, DateTimeOffset? dateFrom = null, DateTimeOffset? dateTo = null, int page = 1, int pageSize = 20);
    }
}
