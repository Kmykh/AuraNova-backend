using System.Net;
using System.Net.Http.Json;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraNova.IntegrationTests
{
    /// <summary>Tarjeta con dedicatoria: el diseño completo y la foto propia llegan al pedido.</summary>
    public class CardDesignOrderTests
    {
        private const string Photo = "data:image/webp;base64,UklGRg==";

        /// <summary>Guarda "fotos" en memoria y registra lo que se borra.</summary>
        private class FakeImages : IReferenceImageService
        {
            public List<string> Saved { get; } = new();

            public Task<string?> PersistAsync(string? reference, CancellationToken cancellationToken = default) =>
                Task.FromResult<string?>(null);

            public Task<string> PersistFileAsync(Stream content, string contentType, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<string> PersistCardBackgroundAsync(string dataUrl, CancellationToken cancellationToken = default)
            {
                var path = $"private/custom-orders/cards/{Guid.NewGuid()}.webp";
                Saved.Add(path);
                return Task.FromResult(path);
            }
        }

        private class FakeStorage : IFileStorageService
        {
            public List<string> Deleted { get; } = new();
            public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder) => throw new NotSupportedException();
            public Task DeleteAsync(string path) { Deleted.Add(path); return Task.CompletedTask; }
            public Task<Stream?> DownloadAsync(string path) => Task.FromResult<Stream?>(null);
            public Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(IEnumerable<string> storedPaths) =>
                Task.FromResult<IReadOnlyDictionary<string, string>>(storedPaths.Distinct().ToDictionary(p => p, p => "https://signed/" + p));
            public Task<IReadOnlyList<StoredFileInfo>> ListAsync(string folder) => Task.FromResult<IReadOnlyList<StoredFileInfo>>([]);
        }

        private static (WebApplicationFactory<Program> Factory, FakeImages Images, FakeStorage Storage) Setup()
        {
            var dbName = Guid.NewGuid().ToString();
            var images = new FakeImages();
            var storage = new FakeStorage();
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JwtSettings:SecretKey", "TestSecretKeyForIntegrationTestsMustBeLongEnough123456");
                builder.UseSetting("JwtSettings:Issuer", "AuraNova.Test");
                builder.UseSetting("JwtSettings:Audience", "AuraNova.Test");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
                builder.UseSetting("InitialAdmin:Enabled", "false");
                builder.ConfigureServices(services =>
                {
                    services.Remove(services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)));
                    services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
                    services.Remove(services.Single(d => d.ServiceType == typeof(IReferenceImageService)));
                    services.AddSingleton<IReferenceImageService>(images);
                    services.Remove(services.Single(d => d.ServiceType == typeof(IFileStorageService)));
                    services.AddSingleton<IFileStorageService>(storage);
                });
            });
            return (factory, images, storage);
        }

        private static async Task<(Guid ProductId, Guid PointId)> Seed(WebApplicationFactory<Program> factory, bool allowsCard = true)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = new Product { Name = "Ramo eterno", Price = 60m, Stock = 5, IsAvailable = true, AllowsPhraseCard = allowsCard };
            var point = new MeetingPoint { Name = "Plaza", Address = "Centro", Cost = 0m, IsActive = true };
            db.Products.Add(product);
            db.MeetingPoints.Add(point);
            await db.SaveChangesAsync();
            return (product.Id, point.Id);
        }

        private static object Order(Guid productId, Guid pointId, object item) => new
        {
            customer = new { name = "Lucía Ramos", phone = "987654321" },
            delivery = new { type = "MeetingPoint", meetingPointId = pointId },
            items = new[] { item }
        };

        [Fact]
        public async Task CardWithOwnPhoto_IsStoredWithTheFullDesign()
        {
            var (factory, images, _) = Setup();
            var (productId, pointId) = await Seed(factory);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/orders", Order(productId, pointId, new
            {
                productId, quantity = 1, hasPhraseCard = true,
                phraseText = "Feliz aniversario", phraseFont = "Clásica", phraseTo = "  Ana ", phraseFrom = "Luis",
                phraseTheme = "FOTO", cardBackgroundImage = Photo,
                // El cliente no puede elegir una ruta del bucket
                cardBackgroundPath = "private/payment-evidence/otro-pedido.webp"
            }));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var item = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OrderItems.SingleAsync();
            Assert.Equal(("Feliz aniversario", "Ana", "Luis", "foto"), (item.PhraseText, item.PhraseTo, item.PhraseFrom, item.PhraseTheme));
            Assert.Equal(images.Saved.Single(), item.CardBackgroundPath);
        }

        [Fact]
        public async Task UnknownTheme_IsDropped_AndPhotoIsDeletedWhenTheOrderFails()
        {
            var (factory, images, storage) = Setup();
            var (productId, pointId) = await Seed(factory);
            using var client = factory.CreateClient();

            var ok = await client.PostAsJsonAsync("/api/orders", Order(productId, pointId, new
            {
                productId, quantity = 1, hasPhraseCard = true, phraseText = "Te quiero", phraseTheme = "<script>"
            }));
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
            using (var scope = factory.Services.CreateScope())
                Assert.Null((await scope.ServiceProvider.GetRequiredService<AppDbContext>().OrderItems.SingleAsync()).PhraseTheme);

            // Pide más stock del que hay: el pedido falla y la foto ya subida se borra
            var failed = await client.PostAsJsonAsync("/api/orders", Order(productId, pointId, new
            {
                productId, quantity = 50, hasPhraseCard = true, phraseText = "Hola", phraseTheme = "foto", cardBackgroundImage = Photo
            }));
            Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
            Assert.Equal(images.Saved, storage.Deleted);
        }
    }
}
