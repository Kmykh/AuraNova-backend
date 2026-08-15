using System.Threading.Tasks;
using AuraNova.Application.Auth.DTOs;

namespace AuraNova.Application.Auth.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
    }
}