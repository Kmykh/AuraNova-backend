using System;
using System.Threading.Tasks;
using AuraNova.Application.Common.Models;

namespace AuraNova.Application.Audit.DTOs
{
    public class AdminAuditEntry
    {
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? Description { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public class AdminAuditLogResponse
    {
        public Guid Id { get; set; }
        public Guid AdminUserId { get; set; }
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? Description { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
