using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Audit.DTOs;
using AuraNova.Application.Audit.Interfaces;
using AuraNova.Application.Common.Models;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.Audit
{
    public class AdminAuditService : IAdminAuditService
    {
        private readonly AppDbContext _db;

        public AdminAuditService(AppDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(Guid adminUserId, AdminAuditEntry entry)
        {
            var auditLog = new AdminAuditLog
            {
                Id = Guid.NewGuid(),
                AdminUserId = adminUserId,
                Action = entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                Description = entry.Description,
                IpAddress = entry.IpAddress,
                UserAgent = entry.UserAgent
            };

            _db.AdminAuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();
        }

        public async Task<PagedResponse<AdminAuditLogResponse>> GetAuditLogsAsync(string? action = null, string? entityType = null, Guid? adminUserId = null, DateTimeOffset? dateFrom = null, DateTimeOffset? dateTo = null, int page = 1, int pageSize = 20)
        {
            var query = _db.AdminAuditLogs.AsNoTracking().Include(x => x.AdminUser).AsQueryable();

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(x => x.Action == action);

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(x => x.EntityType == entityType);

            if (adminUserId.HasValue)
                query = query.Where(x => x.AdminUserId == adminUserId.Value);

            if (dateFrom.HasValue)
                query = query.Where(x => x.CreatedAt >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(x => x.CreatedAt <= dateTo.Value);

            var total = await query.CountAsync();
            
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminAuditLogResponse
                {
                    Id = x.Id,
                    AdminUserId = x.AdminUserId,
                    AdminEmail = x.AdminUser.Email,
                    AdminName = x.AdminUser.Name,
                    Action = x.Action,
                    EntityType = x.EntityType,
                    EntityId = x.EntityId,
                    Description = x.Description,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return new PagedResponse<AdminAuditLogResponse>(items, total, page, pageSize);
        }
    }
}
