using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraNova.IntegrationTests
{
    /// <summary>
    /// El límite de login debe ser por cliente: un atacante agotando su cuota no puede bloquear al administrador.
    /// </summary>
    public class RateLimitingIntegrationTests
    {
        private const string ClientIpHeader = "X-Test-Client-IP";

        private static WebApplicationFactory<Program> CreateFactory()
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JwtSettings:SecretKey", "TestSecretKeyForIntegrationTestsMustBeLongEnough123456");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
                builder.UseSetting("JwtSettings:Issuer", "Test");
                builder.UseSetting("JwtSettings:Audience", "Test");
                builder.UseSetting("Security:ClientIpHeader", ClientIpHeader);

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

        private static Task<HttpResponseMessage> LoginFrom(HttpClient client, string ip)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = new StringContent("""{"email":"x@x.com","password":"wrong"}""", System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add(ClientIpHeader, ip);
            return client.SendAsync(request);
        }

        [Fact]
        public async Task Login_LimitIsPerClient_AttackerDoesNotBlockOthers()
        {
            using var factory = CreateFactory();
            var client = factory.CreateClient();

            for (var i = 0; i < 5; i++)
                Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginFrom(client, "203.0.113.10")).StatusCode);

            Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginFrom(client, "203.0.113.10")).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginFrom(client, "198.51.100.20")).StatusCode);
        }
    }
}
