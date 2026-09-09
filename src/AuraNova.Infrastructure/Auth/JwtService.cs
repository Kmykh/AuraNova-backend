using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AuraNova.Application.Auth.Interfaces;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Auth
{
    public class JwtSettings
    {
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
        public int ExpirationMinutes { get; set; }
    }

    public class JwtService : IJwtService
    {
        private readonly JwtSettings _settings;
        private readonly byte[] _key;

        public JwtService(IOptions<JwtSettings> settings)
        {
            _settings = settings.Value;
            _key = Encoding.UTF8.GetBytes(_settings.SecretKey);
        }

        public string GenerateToken(AdminUser user)
        {
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Admin" : user.Role;
            // Capitalize role to ensure it matches exactly with [Authorize(Roles = "Admin")]
            if (userRole.Equals("admin", StringComparison.OrdinalIgnoreCase)) userRole = "Admin";
            if (userRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase)) userRole = "SuperAdmin";

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name),
                new Claim(ClaimTypes.Role, userRole),
                new Claim("role", userRole) // Add a simple role claim for frontend decoding
            };

            // If user is SuperAdmin, they should also inherit the Admin role to access Admin endpoints
            if (userRole == "SuperAdmin")
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}