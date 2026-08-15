using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuraNova.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AuraNova.UnitTests
{
    public class SupabaseStorageServiceTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private SupabaseStorageService CreateService(MockHttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://test.supabase.co") // Won't be used directly by service since it sets it, but good for safety
            };
            var options = Options.Create(new SupabaseSettings { Url = "https://test.supabase.co", ServiceRoleKey = "fake-key" });
            return new SupabaseStorageService(httpClient, options, new NullLogger<SupabaseStorageService>());
        }

        [Fact]
        public async Task UploadAsync_ShouldSendCorrectRequestAndReturnPath()
        {
            var handler = new MockHttpMessageHandler();
            var service = CreateService(handler);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake image"));
            var path = await service.UploadAsync(stream, "evidence.jpg", "image/jpeg", "payment-evidence/123");

            Assert.NotNull(handler.LastRequest);
            Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
            
            // Path should end with a new GUID and .jpg
            Assert.StartsWith("payment-evidence/123/", path);
            Assert.EndsWith(".jpg", path);

            // Request URI should contain the bucket and path
            var uri = handler.LastRequest.RequestUri!.ToString();
            Assert.Contains("object/payment-evidence/", uri);
            
            // Verify Authorization header
            Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
            Assert.Equal("fake-key", handler.LastRequest.Headers.Authorization?.Parameter);
        }

        [Fact]
        public async Task DeleteAsync_ShouldSendDeleteRequest()
        {
            var handler = new MockHttpMessageHandler();
            var service = CreateService(handler);

            var path = "payment-evidence/123/fake-uuid.jpg";
            await service.DeleteAsync(path);

            Assert.NotNull(handler.LastRequest);
            Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
            
            var uri = handler.LastRequest.RequestUri!.ToString();
            Assert.Contains(path, uri);
        }
    }
}
