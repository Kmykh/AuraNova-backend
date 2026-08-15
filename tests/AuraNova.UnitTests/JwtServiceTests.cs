using System;
using System.IdentityModel.Tokens.Jwt;
using AuraNova.Infrastructure.Auth;
using AuraNova.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace AuraNova.UnitTests
{
    public class JwtServiceTests
    {
        [Fact]
        public void GenerateToken_ShouldContainExpectedClaims()
        {
            var settings = new JwtSettings
            {
                Issuer = "AuraNova.API",
                Audience = "AuraNova.Admin",
                SecretKey = "ThisIsALongSecretKeyForTestingPurposesOnly_ChangeInProduction",
                ExpirationMinutes = 60
            };

            var options = Options.Create(settings);
            var service = new JwtService(options);

            var admin = new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = "admin@auranova.test",
                Name = "Test Admin",
                IsActive = true
            };

            var token = service.GenerateToken(admin);
            Assert.False(string.IsNullOrWhiteSpace(token));

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            Assert.Equal(settings.Issuer, jwt.Issuer);
            Assert.Equal(settings.Audience, jwt.Audiences.First());

            var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            Assert.Equal(admin.Id.ToString(), sub);

            var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
            Assert.Equal(admin.Email, email);

            var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            Assert.Equal(admin.Name, name);

            var role = jwt.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
            Assert.Equal("Admin", role);
        }
    }
}
