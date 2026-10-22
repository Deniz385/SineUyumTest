using SineUyum.Api.Exceptions;
using SkiaSharp;

namespace SineUyum.Api.Services.Media
{
    // NEVER stores an uploaded profile photo as is: decodes it, rotates it by its
    // EXIF orientation, crops a centered square and RE-ENCODES it as a 512x512
    // JPEG. This way:
    //   * all metadata including the EXIF/GPS location is removed (privacy),
    //   * files disguised as images (polyglots, HTML, scripts) are dropped; the
    //     output is always a clean JPEG we produced ourselves,
    //   * size and format are fixed (consistent look in clients, small files).
    public static class AvatarImageProcessor
    {
        public const int OutputSize = 512;
        public const int JpegQuality = 85;

        // The size in the header is checked (before decoding pixels): a small file
        // declaring 50000x50000 ("decompression bomb") must not ask for gigabytes of memory.
        public const long MaxDeclaredPixels = 100_000_000;

        // The real memory limit applies to the size AFTER scaled decoding. JPEG
        // can be decoded at 1/2..1/8 scale, so 48 MP phone photos pass; very large
        // images in formats that cannot be scaled (PNG) are rejected.
        public const long MaxDecodedPixels = 25_000_000;

        private static readonly HashSet<SKEncodedImageFormat> Allowed = new()
        {
            SKEncodedImageFormat.Jpeg,
            SKEncodedImageFormat.Png,
            SKEncodedImageFormat.Webp,
        };

        public static byte[] Process(byte[] input)
        {
            using var data = SKData.CreateCopy(input);
            using var codec = SKCodec.Create(data)
                ?? throw new DomainException("profile.photoNotAnImage", StatusCodes.Status415UnsupportedMediaType);

            if (!Allowed.Contains(codec.EncodedFormat))
            {
                throw new DomainException(
                    "profile.photoUnsupportedFormat",
                    StatusCodes.Status415UnsupportedMediaType);
            }

            var declared = codec.Info;
            if (declared.Width <= 0 || declared.Height <= 0 ||
                (long)declared.Width * declared.Height > MaxDeclaredPixels)
            {
                throw new DomainException("profile.photoDimensionsTooLarge");
            }

            // Decoded shrunk just enough for the short side to fit the output (in formats
            // that support it). The codec rounds an unsupported ratio to the nearest supported one.
            var scale = Math.Min(1f, (float)OutputSize / Math.Min(declared.Width, declared.Height));
            var decodeSize = codec.GetScaledDimensions(scale);
            if ((long)decodeSize.Width * decodeSize.Height > MaxDecodedPixels)
            {
                throw new DomainException("profile.photoDimensionsTooLarge");
            }

            var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var decoded = new SKBitmap(decodeInfo);
            var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
            // IncompleteInput: a truncated but partly readable file; the part read is used.
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            {
                throw new DomainException("profile.photoDecodeFailed", StatusCodes.Status415UnsupportedMediaType);
            }

            using var oriented = ApplyOrigin(decoded, codec.EncodedOrigin);
            return CropResizeEncode(oriented);
        }

        private static byte[] CropResizeEncode(SKBitmap source)
        {
            var side = Math.Min(source.Width, source.Height);
            var src = SKRect.Create((source.Width - side) / 2f, (source.Height - side) / 2f, side, side);
            var dst = SKRect.Create(0, 0, OutputSize, OutputSize);

            var info = new SKImageInfo(OutputSize, OutputSize, SKColorType.Rgba8888, SKAlphaType.Opaque);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            // JPEG has no transparency; transparent PNGs land on a white background.
            canvas.Clear(SKColors.White);
            using var image = SKImage.FromBitmap(source);
            canvas.DrawImage(image, src, dst, new SKSamplingOptions(SKCubicResampler.Mitchell));

            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            return encoded.ToArray();
        }

        // EXIF orientation (phone cameras only tag it, without rotating the pixels).
        // Each orientation is given as a source point (x, y) → target point mapping
        // (w, h: source size):
        //   TopRight (2)    horizontal flip (w-x, y)
        //   BottomRight (3) 180 degrees     (w-x, h-y)
        //   BottomLeft (4)  vertical flip   (x, h-y)
        //   LeftTop (5)     transpose       (y, x)
        //   RightTop (6)    90 deg CW       (h-y, x)
        //   RightBottom (7) transverse      (h-y, w-x)
        //   LeftBottom (8)  90 deg CCW      (y, w-x)
        // SKMatrix: x' = sx*x + kx*y + tx ;  y' = ky*x + sy*y + ty
        internal static SKBitmap ApplyOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            float w = bitmap.Width, h = bitmap.Height;
            SKMatrix? matrix = origin switch
            {
                SKEncodedOrigin.TopRight => Affine(-1, 0, w, 0, 1, 0),
                SKEncodedOrigin.BottomRight => Affine(-1, 0, w, 0, -1, h),
                SKEncodedOrigin.BottomLeft => Affine(1, 0, 0, 0, -1, h),
                SKEncodedOrigin.LeftTop => Affine(0, 1, 0, 1, 0, 0),
                SKEncodedOrigin.RightTop => Affine(0, -1, h, 1, 0, 0),
                SKEncodedOrigin.RightBottom => Affine(0, -1, h, -1, 0, w),
                SKEncodedOrigin.LeftBottom => Affine(0, 1, 0, -1, 0, w),
                _ => null, // TopLeft: as is
            };
            if (matrix == null) return bitmap.Copy();

            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var result = new SKBitmap(new SKImageInfo(
                swap ? bitmap.Height : bitmap.Width,
                swap ? bitmap.Width : bitmap.Height,
                bitmap.ColorType, bitmap.AlphaType));

            using var canvas = new SKCanvas(result);
            canvas.SetMatrix(matrix.Value);
            // 90-degree/flip transforms are pixel exact; nearest neighbour is lossless.
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            return result;
        }

        private static SKMatrix Affine(float sx, float kx, float tx, float ky, float sy, float ty) =>
            new(sx, kx, tx, ky, sy, ty, 0, 0, 1);
    }
}
