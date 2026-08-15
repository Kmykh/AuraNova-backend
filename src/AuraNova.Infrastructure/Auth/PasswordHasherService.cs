using AuraNova.Application.Auth.Interfaces;
using AuraNova.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AuraNova.Infrastructure.Auth
{
    public class PasswordHasherService : IPasswordHasherService
    {
        private readonly PasswordHasher<AdminUser> _hasher = new PasswordHasher<AdminUser>();

        public string HashPassword(AdminUser user, string password)
        {
            return _hasher.HashPassword(user, password);
        }

        public bool VerifyPassword(AdminUser user, string password, string passwordHash)
        {
            var result = _hasher.VerifyHashedPassword(user, passwordHash, password);
            return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
