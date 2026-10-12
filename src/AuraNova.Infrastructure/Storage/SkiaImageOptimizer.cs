using AuraNova.Application.Storage.Interfaces;
using SkiaSharp;

namespace AuraNova.Infrastructure.Storage
{
    /// <summary>
    /// Optimizador basado en SkiaSharp (MIT). Solo acepta JPEG, PNG y WebP: re-codificar todo lo que se sube
    /// también neutraliza archivos disfrazados (polyglots) que pasaban la validación por extensión/MIME.
    /// </summary>
    public class SkiaImageOptimizer : IImageOptimizer
    {
        // Evita "decompression bombs": un PNG pequeño en bytes puede declarar dimensiones gigantes.
        private const long MaxPixels = 40_000_000;

        private static readonly HashSet<SKEncodedImageFormat> AllowedFormats =
        [
            SKEncodedImageFormat.Jpeg,
            SKEncodedImageFormat.Png,
            SKEncodedImageFormat.Webp
        ];

        public async Task<OptimizedImage> OptimizeAsync(Stream input, ImageProfile profile, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            if (input.CanSeek) input.Position = 0;
            await input.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            using var data = SKData.Create(buffer);
            using var codec = data == null ? null : SKCodec.Create(data);
            if (codec == null || !AllowedFormats.Contains(codec.EncodedFormat))
                throw new InvalidImageException("El archivo no es una imagen válida (se aceptan JPG, PNG y WEBP).");

            var info = codec.Info;
            if ((long)info.Width * info.Height > MaxPixels)
                throw new InvalidImageException("La imagen tiene dimensiones demasiado grandes.");

            cancellationToken.ThrowIfCancellationRequested();

            using var decoded = Decode(codec, profile.MaxDimension);
            using var oriented = ApplyOrigin(decoded, codec.EncodedOrigin);
            using var resized = FitWithin(oriented, profile.MaxDimension);

            using var pixmap = resized.PeekPixels();
            var options = new SKWebpEncoderOptions(
                profile.Lossless ? SKWebpEncoderCompression.Lossless : SKWebpEncoderCompression.Lossy,
                profile.Quality);
            using var encoded = pixmap.Encode(options)
                ?? throw new InvalidImageException("No se pudo procesar la imagen.");

            var output = new MemoryStream(encoded.ToArray());
            return new OptimizedImage(output, "image/webp", ".webp", resized.Width, resized.Height);
        }

        private static SKBitmap Decode(SKCodec codec, int maxDimension)
        {
            var info = codec.Info;
            var size = new SKSizeI(info.Width, info.Height);

            // JPEG puede decodificarse directamente a 1/2, 1/4 u 1/8: menos memoria y CPU.
            var longest = Math.Max(info.Width, info.Height);
            if (longest > maxDimension)
            {
                var scaled = codec.GetScaledDimensions((float)maxDimension / longest);
                if (Math.Max(scaled.Width, scaled.Height) >= maxDimension)
                    size = scaled;
            }

            var decodeInfo = new SKImageInfo(
                size.Width,
                size.Height,
                SKColorType.Rgba8888,
                info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul,
                SKColorSpace.CreateSrgb()); // convierte perfiles como Display P3 (iPhone) a sRGB

            var bitmap = new SKBitmap(decodeInfo);
            var result = codec.GetPixels(decodeInfo, bitmap.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                bitmap.Dispose();
                throw new InvalidImageException("La imagen está dañada o no se puede leer.");
            }

            return bitmap;
        }

        /// <summary>Aplica la orientación EXIF (fotos de celular) porque el WebP final no conserva metadatos.</summary>
        private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft)
                return source.Copy();

            float w = source.Width, h = source.Height;
            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

            // x' = ScaleX*x + SkewX*y + TransX ; y' = SkewY*x + ScaleY*y + TransY
            var matrix = origin switch
            {
                SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
                SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
                SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
                _ => SKMatrix.Identity
            };

            var target = new SKBitmap(source.Info.WithSize(
                swap ? source.Height : source.Width,
                swap ? source.Width : source.Height));

            using var image = SKImage.FromBitmap(source);
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(matrix);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            canvas.Flush();
            return target;
        }

        private static SKBitmap FitWithin(SKBitmap source, int maxDimension)
        {
            var longest = Math.Max(source.Width, source.Height);
            if (longest <= maxDimension)
                return source.Copy();

            var ratio = (double)maxDimension / longest;
            var width = Math.Max(1, (int)Math.Round(source.Width * ratio));
            var height = Math.Max(1, (int)Math.Round(source.Height * ratio));

            return source.Resize(source.Info.WithSize(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
                ?? throw new InvalidImageException("No se pudo redimensionar la imagen.");
        }
    }
}
