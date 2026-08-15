using AuraNova.Domain.Entities;

namespace AuraNova.Application.Auth.Interfaces
{
    public interface IPasswordHasherService
    {
        string HashPassword(AdminUser user, string password);
        bool VerifyPassword(AdminUser user, string password, string passwordHash);
    }
}