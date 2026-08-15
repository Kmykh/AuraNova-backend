using System.Threading.Tasks;
using AuraNova.Application.Auth.DTOs;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Auth
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasherService _hasher;
        private readonly IJwtService _jwt;
        private readonly JwtSettings _jwtSettings;

        public AuthService(AppDbContext db, IPasswordHasherService hasher, IJwtService jwt, IOptions<JwtSettings> jwtOptions)
        {
            _db = db;
            _hasher = hasher;
            _jwt = jwt;
            _jwtSettings = jwtOptions.Value;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var email = request.Email?.Trim().ToLowerInvariant();
            var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null)
                return null;

            if (!user.IsActive)
                return null;

            if (!_hasher.VerifyPassword(user, request.Password, user.PasswordHash))
                return null;

            var token = _jwt.GenerateToken(user);

            return new LoginResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresAt = System.DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email
                }
            };
        }
    }
}
