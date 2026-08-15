using System;

namespace AuraNova.Domain.Entities
{
    public class AdminAuditLog
    {
        public Guid Id { get; set; }
        public Guid AdminUserId { get; set; }
        
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        
        public string? Description { get; set; }
        
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; }
        
        public AdminUser AdminUser { get; set; } = null!;

        public AdminAuditLog()
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
