using AuraNova.Domain.Entities;

namespace AuraNova.Application.Auth.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(AdminUser user);
    }
}