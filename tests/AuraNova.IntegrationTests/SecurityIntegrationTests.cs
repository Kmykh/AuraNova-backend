using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AuraNova.IntegrationTests
{
    public class SecurityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public SecurityIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task SecurityHeaders_ArePresent_InResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/health");

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.True(response.Headers.Contains("X-Content-Type-Options"));
            Assert.True(response.Headers.Contains("X-Frame-Options"));
            Assert.True(response.Headers.Contains("X-XSS-Protection"));
            Assert.True(response.Headers.Contains("Content-Security-Policy"));
            
            var xContentTypeOptions = response.Headers.GetValues("X-Content-Type-Options");
            Assert.Contains("nosniff", xContentTypeOptions);
        }
        
        [Fact]
        public async Task ProblemDetails_ReturnsRfc7807Format_OnValidationError()
        {
            // Arrange
            var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/auth/login", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }
}
