using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AuraNova.Application.Dashboard.DTOs;
using Xunit;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.IntegrationTests
{
    public class DashboardIntegrationTests
    {
        private WebApplicationFactory<Program> CreateFactory()
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JwtSettings:SecretKey", "TestSecretKeyForIntegrationTestsMustBeLongEnough123456");
                builder.UseSetting("JwtSettings:Issuer", "Test");
                builder.UseSetting("JwtSettings:Audience", "Test");
                
                builder.ConfigureServices(services =>
                {
                    var descriptor = System.Linq.Enumerable.SingleOrDefault(services,
                        d => d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<AuraNova.Infrastructure.Persistence.AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<AuraNova.Infrastructure.Persistence.AppDbContext>(options =>
                        Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions.UseInMemoryDatabase(options, System.Guid.NewGuid().ToString()));
                });
            });
        }

        private System.Net.Http.HttpClient CreateClientWithAuth(WebApplicationFactory<Program> factory)
        {
            var client = factory.CreateClient();
            var key = System.Text.Encoding.UTF8.GetBytes("TestSecretKeyForIntegrationTestsMustBeLongEnough123456");
            var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, System.Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
            };

            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(claims),
                Expires = System.DateTime.UtcNow.AddMinutes(60),
                Issuer = "Test",
                Audience = "Test",
                SigningCredentials = credentials
            };

            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            return client;
        }

        [Fact]
        public async Task GetSummary_WithoutToken_ReturnsUnauthorized()
        {
            using var factory = CreateFactory();
            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/admin/dashboard/summary");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetSummary_WithAdminToken_ReturnsSuccessAndData()
        {
            using var factory = CreateFactory();
            var client = CreateClientWithAuth(factory);
            var response = await client.GetAsync("/api/admin/dashboard/summary");

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
            Assert.NotNull(data);
            Assert.NotNull(data.Orders);
            Assert.NotNull(data.Quotes);
            Assert.NotNull(data.Payments);
            Assert.NotNull(data.Today);
        }
    }
}
