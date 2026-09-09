using System;

namespace AuraNova.Domain.Entities
{
    public class AdminUser
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "Admin";
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public AdminUser()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            IsActive = true;
        }
    }
}