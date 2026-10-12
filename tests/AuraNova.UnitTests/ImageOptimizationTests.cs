using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AuraNova.Application.Storage.Interfaces;
using AuraNova.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Xunit;

namespace AuraNova.UnitTests
{
    public class ImageOptimizationTests
    {
        private static MemoryStream CreateImage(int width, int height, SKEncodedImageFormat format, bool transparent = false)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, transparent ? SKAlphaType.Premul : SKAlphaType.Opaque));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(transparent ? SKColors.Transparent : SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Goldenrod };
                canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
            }

            using var data = bitmap.Encode(format, 95);
            return new MemoryStream(data.ToArray());
        }

        private class RecordingStorage : IFileStorageService
        {
            public string? FileName { get; private set; }
            public string? ContentType { get; private set; }
            public string? Folder { get; private set; }
            public byte[] Content { get; private set; } = [];

            public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder)
            {
                using var copy = new MemoryStream();
                await fileStream.CopyToAsync(copy);
                Content = copy.ToArray();
                FileName = fileName;
                ContentType = contentType;
                Folder = folder;
                return $"{folder}/{Path.GetFileNameWithoutExtension(fileName)}-guid{Path.GetExtension(fileName)}";
            }

            public Task DeleteAsync(string path) => Task.CompletedTask;
            public Task<Stream?> DownloadAsync(string path) => Task.FromResult<Stream?>(null);
            public Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(IEnumerable<string> storedPaths) =>
                Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
            public Task<IReadOnlyList<StoredFileInfo>> ListAsync(string folder) =>
                Task.FromResult<IReadOnlyList<StoredFileInfo>>([]);
        }

        [Fact]
        public async Task Optimizer_LargeJpeg_IsResizedAndConvertedToWebp()
        {
            var optimizer = new SkiaImageOptimizer();
            using var input = CreateImage(3000, 2000, SKEncodedImageFormat.Jpeg);

            using var result = await optimizer.OptimizeAsync(input, ImageProfile.Catalog);

            Assert.Equal("image/webp", result.ContentType);
            Assert.Equal(".webp", result.Extension);
            Assert.Equal(1600, result.Width);
            Assert.Equal(1067, result.Height);

            using var codec = SKCodec.Create(new MemoryStream(result.Content.ToArray()));
            Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        }

        [Fact]
        public async Task Optimizer_SmallImage_IsNotUpscaled()
        {
            var optimizer = new SkiaImageOptimizer();
            using var input = CreateImage(800, 600, SKEncodedImageFormat.Png);

            using var result = await optimizer.OptimizeAsync(input, ImageProfile.Catalog);

            Assert.Equal(800, result.Width);
            Assert.Equal(600, result.Height);
        }

        [Fact]
        public async Task Optimizer_TransparentPngQr_KeepsDimensionsLossless()
        {
            var optimizer = new SkiaImageOptimizer();
            using var input = CreateImage(512, 512, SKEncodedImageFormat.Png, transparent: true);

            using var result = await optimizer.OptimizeAsync(input, ImageProfile.Qr);

            Assert.Equal(512, result.Width);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public async Task Optimizer_NonImageContent_ThrowsInvalidImage()
        {
            var optimizer = new SkiaImageOptimizer();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("<script>alert(1)</script> disguised as .jpg"));

            await Assert.ThrowsAsync<InvalidImageException>(() => optimizer.OptimizeAsync(input, ImageProfile.Document));
        }

        [Theory]
        [InlineData("products", "catalog")]
        [InlineData("custom-orders", "catalog")]
        [InlineData("business-settings/yape-qr", "qr")]
        [InlineData("payment-evidence/123", "document")]
        [InlineData("shipping-evidence", "document")]
        public void ImageProfile_ForFolder_SelectsExpectedProfile(string folder, string expected)
        {
            Assert.Equal(expected, ImageProfile.ForFolder(folder).Name);
        }

        [Fact]
        public async Task OptimizingStorage_Image_UploadsWebp()
        {
            var inner = new RecordingStorage();
            var storage = new OptimizingFileStorageService(inner, new SkiaImageOptimizer(), NullLogger<OptimizingFileStorageService>.Instance);
            using var input = CreateImage(2400, 1800, SKEncodedImageFormat.Jpeg);

            var path = await storage.UploadAsync(input, "photo.jpg", "image/jpeg", "products");

            Assert.Equal("image/webp", inner.ContentType);
            Assert.Equal("photo.webp", inner.FileName);
            Assert.Equal("products", inner.Folder);
            Assert.EndsWith(".webp", path);
            Assert.True(inner.Content.Length < input.Length);
        }

        [Fact]
        public async Task OptimizingStorage_NonImage_PassesThrough()
        {
            var inner = new RecordingStorage();
            var storage = new OptimizingFileStorageService(inner, new SkiaImageOptimizer(), NullLogger<OptimizingFileStorageService>.Instance);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7"));

            await storage.UploadAsync(input, "guia.pdf", "application/pdf", "shipping-evidence");

            Assert.Equal("application/pdf", inner.ContentType);
            Assert.Equal("guia.pdf", inner.FileName);
        }

        [Fact]
        public void UrlResolver_MapsPathsAndKeepsAbsoluteValues()
        {
            var resolver = TestUrlResolver.Instance;

            Assert.Equal($"{TestUrlResolver.PublicBase}/products/a.webp", resolver.ToPublicUrl("products/a.webp"));
            Assert.Equal($"{TestUrlResolver.PublicBase}/products/a.webp", resolver.ToPublicUrl("/products/a.webp"));
            Assert.Equal("https://otro.cdn/x.png", resolver.ToPublicUrl("https://otro.cdn/x.png"));
            Assert.Equal("data:image/png;base64,AAAA", resolver.ToPublicUrl("data:image/png;base64,AAAA"));
            Assert.Null(resolver.ToPublicUrl(null));

            Assert.Equal("products/a.webp", resolver.ToStoragePath($"{TestUrlResolver.PublicBase}/products/a.webp"));
            Assert.Equal("https://otro.cdn/x.png", resolver.ToStoragePath("https://otro.cdn/x.png"));
        }

        [Fact]
        public void UrlResolver_UsesPublicBaseUrlOverride()
        {
            var resolver = new StorageUrlResolver(Options.Create(new SupabaseSettings
            {
                Url = "https://test.supabase.co",
                PublicBaseUrl = "https://cdn.auranova.pe/"
            }));

            Assert.Equal("https://cdn.auranova.pe/products/a.webp", resolver.ToPublicUrl("products/a.webp"));
        }

        [Fact]
        public async Task ReferenceImage_DataUrl_IsStoredAsFile()
        {
            var inner = new RecordingStorage();
            var service = new ReferenceImageService(inner);
            using var image = CreateImage(400, 300, SKEncodedImageFormat.Png);
            var dataUrl = "data:image/png;base64," + Convert.ToBase64String(image.ToArray());

            var path = await service.PersistAsync(dataUrl);

            Assert.NotNull(path);
            Assert.StartsWith("custom-orders/", path);
            Assert.Equal("image/png", inner.ContentType);
        }

        [Fact]
        public async Task ReferenceImage_NullOrEmpty_ReturnsNull()
        {
            var service = new ReferenceImageService(new RecordingStorage());

            Assert.Null(await service.PersistAsync(null));
            Assert.Null(await service.PersistAsync("  "));
        }

        [Theory]
        [InlineData("https://evil.example/tracker.png")]
        [InlineData("payment-evidence/otro-pedido/comprobante.webp")]
        [InlineData("private/custom-orders/de-otro-cliente.webp")]
        [InlineData("https://test.supabase.co/storage/v1/object/public/payment-evidence/custom-orders/abc.webp")]
        [InlineData("data:text/html;base64,PGgxPmhpPC9oMT4=")]
        [InlineData("data:image/png;base64,@@not-base64@@")]
        public async Task ReferenceImage_InvalidValues_Throw(string value)
        {
            var service = new ReferenceImageService(new RecordingStorage());

            await Assert.ThrowsAsync<InvalidImageException>(() => service.PersistAsync(value));
        }
    }
}
