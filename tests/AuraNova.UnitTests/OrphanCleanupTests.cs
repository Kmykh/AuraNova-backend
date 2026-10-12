using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Infrastructure.Persistence;
using AuraNova.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class OrphanCleanupTests
    {
        private static AppDbContext GetInMemoryDb() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static ImageMaintenanceService CreateService(AppDbContext db, TestStorage storage) =>
            new(db, storage, new SkiaImageOptimizer(), new Mock<IReferenceImageService>().Object,
                TestUrlResolver.Instance, new NullLogger<ImageMaintenanceService>());

        [Fact]
        public async Task Cleanup_DeletesOnlyUnusedOldFiles()
        {
            using var db = GetInMemoryDb();
            db.Products.AddRange(
                new Product { Name = "A", Price = 1, ImageUrl = "products/in-use.webp" },
                new Product { Name = "B", Price = 1, ImageUrl = $"{TestUrlResolver.PublicBase}/products/in-use-absolute.webp" });
            await db.SaveChangesAsync();

            var old = DateTimeOffset.UtcNow.AddDays(-3);
            var storage = new TestStorage();
            storage.Files.AddRange([
                new StoredFileInfo("products/in-use.webp", 100, old),
                new StoredFileInfo("products/in-use-absolute.webp", 100, old),
                new StoredFileInfo("products/orphan.png", 2_000_000, old),
                new StoredFileInfo("products/just-uploaded.webp", 150_000, DateTimeOffset.UtcNow.AddMinutes(-5))
            ]);

            var report = await CreateService(db, storage).CleanupOrphanProductImagesAsync(dryRun: false);

            Assert.Equal(["products/orphan.png"], storage.Deleted);
            Assert.Equal(1, report.Processed);
            Assert.Equal(1, report.Skipped);
            Assert.Equal(2_000_000, report.BytesBefore);
        }

        [Fact]
        public async Task Cleanup_DryRun_DeletesNothing()
        {
            using var db = GetInMemoryDb();
            var storage = new TestStorage();
            storage.Files.Add(new StoredFileInfo("products/orphan.png", 10, DateTimeOffset.UtcNow.AddDays(-3)));

            var report = await CreateService(db, storage).CleanupOrphanProductImagesAsync(dryRun: true);

            Assert.Empty(storage.Deleted);
            Assert.Equal(1, report.Processed);
        }

        private class ListHandler : HttpMessageHandler
        {
            public string? LastUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastUri = request.RequestUri!.ToString();
                var json = """
                    [
                      {"name":"a.webp","id":"1","created_at":"2026-10-01T10:00:00Z","metadata":{"size":1234,"mimetype":"image/webp"}},
                      {"name":"subcarpeta","id":null,"created_at":null,"metadata":null}
                    ]
                    """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }
        }

        [Fact]
        public async Task SupabaseList_ParsesFilesAndSkipsFolders()
        {
            var handler = new ListHandler();
            var storage = new SupabaseStorageService(new HttpClient(handler),
                Options.Create(new SupabaseSettings { Url = "https://test.supabase.co", ServiceRoleKey = "k" }),
                new NullLogger<SupabaseStorageService>(), TestUrlResolver.Instance);

            var files = await storage.ListAsync("products");

            var file = Assert.Single(files);
            Assert.Equal("products/a.webp", file.Path);
            Assert.Equal(1234, file.Size);
            Assert.Equal(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), file.CreatedAt);
            Assert.EndsWith("/object/list/payment-evidence", handler.LastUri);
        }
    }
}
