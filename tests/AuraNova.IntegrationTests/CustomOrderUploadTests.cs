using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuraNova.IntegrationTests
{
    public class CustomOrderUploadTests
    {
        private class RecordingStorage : IFileStorageService
        {
            public ConcurrentBag<string> Uploaded { get; } = [];
            public ConcurrentBag<string> Deleted { get; } = [];

            public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder)
            {
                var path = $"private/{folder}/{Guid.NewGuid()}.webp";
                Uploaded.Add(path);
                return Task.FromResult(path);
            }

            public Task DeleteAsync(string path)
            {
                Deleted.Add(path);
                return Task.CompletedTask;
            }

            public Task<Stream?> DownloadAsync(string path) => Task.FromResult<Stream?>(null);

            public Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(IEnumerable<string> storedPaths) =>
                Task.FromResult<IReadOnlyDictionary<string, string>>(storedPaths.Distinct().ToDictionary(p => p, p => $"https://signed.test/{p}"));

            public Task<IReadOnlyList<StoredFileInfo>> ListAsync(string folder) =>
                Task.FromResult<IReadOnlyList<StoredFileInfo>>([]);
        }

        private static WebApplicationFactory<Program> CreateFactory(RecordingStorage storage)
        {
            var dbName = "it_custom_upload_" + Guid.NewGuid();
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JwtSettings:SecretKey", "TestSecretKeyForIntegrationTestsMustBeLongEnough123456");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
                builder.UseSetting("JwtSettings:Issuer", "Test");
                builder.UseSetting("JwtSettings:Audience", "Test");

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);
                    services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));

                    var storageDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileStorageService));
                    if (storageDescriptor != null) services.Remove(storageDescriptor);
                    services.AddSingleton<IFileStorageService>(storage);
                });
            });
        }

        private static string OrderJson(string? link = "https://pin.it/modelo") => JsonSerializer.Serialize(new
        {
            customer = new { name = "Ana Torres", phone = "987654321" },
            delivery = new { type = "NationalShipping", department = "Junín", province = "Huancayo", district = "El Tambo" },
            customizationNotes = "Como la foto pero en tonos lila",
            referenceLinks = link == null ? Array.Empty<string>() : new[] { link },
            desiredDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"),
            occasion = "Cumpleaños",
            budgetRange = "S/ 80 - 120"
        });

        private static MultipartFormDataContent Form(string json, int imageCount)
        {
            var form = new MultipartFormDataContent { { new StringContent(json, Encoding.UTF8), "data" } };
            for (var i = 0; i < imageCount; i++)
            {
                var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, (byte)i]);
                file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(file, "images", $"modelo-{i}.png");
            }
            return form;
        }

        [Fact]
        public async Task Multipart_CreatesOrderWithGallery_AndTrackingByCodeShowsIt()
        {
            var storage = new RecordingStorage();
            await using var factory = CreateFactory(storage);
            var client = factory.CreateClient();

            var response = await client.PostAsync("/api/orders/custom", Form(OrderJson(), 2));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("AN-1001", created.GetProperty("orderCode").GetString());
            Assert.Equal(2, storage.Uploaded.Count);

            var code = created.GetProperty("trackingToken").GetString()!;
            var tracking = await client.GetAsync($"/api/public/tracking/{code.ToLowerInvariant()}");
            Assert.Equal(HttpStatusCode.OK, tracking.StatusCode);

            var json = await tracking.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, json.GetProperty("referenceImages").GetArrayLength());
            Assert.Equal("https://pin.it/modelo", json.GetProperty("referenceLinks")[0].GetString());
            Assert.Equal("Cumpleaños", json.GetProperty("occasion").GetString());
            Assert.DoesNotContain(code, await tracking.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Multipart_TooManyImages_IsRejectedWithoutUploading()
        {
            var storage = new RecordingStorage();
            await using var factory = CreateFactory(storage);

            var response = await factory.CreateClient().PostAsync("/api/orders/custom", Form(OrderJson(), 6));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(storage.Uploaded);
        }

        [Fact]
        public async Task Multipart_InvalidLink_RemovesUploadedImages()
        {
            var storage = new RecordingStorage();
            await using var factory = CreateFactory(storage);

            var response = await factory.CreateClient().PostAsync("/api/orders/custom", Form(OrderJson("javascript:alert(1)"), 2));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(storage.Uploaded.OrderBy(p => p), storage.Deleted.OrderBy(p => p));
        }

        [Fact]
        public async Task Json_LegacyRequest_StillWorks()
        {
            var storage = new RecordingStorage();
            await using var factory = CreateFactory(storage);

            var response = await factory.CreateClient().PostAsync("/api/orders/custom",
                new StringContent(OrderJson(), Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task TrackingByCode_Unknown_Returns404()
        {
            await using var factory = CreateFactory(new RecordingStorage());

            var response = await factory.CreateClient().GetAsync("/api/public/tracking/ZZZZ-ZZZZ");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
