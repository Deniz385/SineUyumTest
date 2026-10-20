using SineUyum.Api.Exceptions;
using SkiaSharp;

namespace SineUyum.Api.Services.Media
{
    // Yüklenen profil fotoğrafını ASLA olduğu gibi saklamaz: çözer, EXIF
    // yönüne göre döndürür, ortadan kare kırpar, 512x512 JPEG olarak YENİDEN
    // kodlar. Böylece:
    //   * EXIF/GPS konumu dahil tüm üst veri silinir (gizlilik),
    //   * resim kılığındaki dosyalar (poliglot, HTML, betik) elenir; çıkan dosya
    //     her zaman bizim ürettiğimiz temiz bir JPEG'dir,
    //   * boyut ve biçim sabittir (istemcilerde tutarlı görünüm, küçük dosya).
    public static class AvatarImageProcessor
    {
        public const int OutputSize = 512;
        public const int JpegQuality = 85;

        // Başlıktaki boyuta bakılır (piksel çözülmeden): 50000x50000 bildiren
        // küçük bir dosya ("decompression bomb") gigabaytlarca bellek istemesin.
        public const long MaxDeclaredPixels = 100_000_000;

        // Asıl bellek sınırı, ölçekli çözümden SONRAKİ boyut üzerinden. JPEG
        // 1/2..1/8 ölçekli çözülebildiği için 48 MP telefon fotoğrafları geçer;
        // ölçeklenemeyen biçimlerde (PNG) çok büyük görseller reddedilir.
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
                ?? throw new DomainException("Dosya bir resim olarak okunamadı.", StatusCodes.Status415UnsupportedMediaType);

            if (!Allowed.Contains(codec.EncodedFormat))
            {
                throw new DomainException(
                    "Desteklenmeyen resim biçimi. JPEG, PNG ya da WebP yükleyin.",
                    StatusCodes.Status415UnsupportedMediaType);
            }

            var declared = codec.Info;
            if (declared.Width <= 0 || declared.Height <= 0 ||
                (long)declared.Width * declared.Height > MaxDeclaredPixels)
            {
                throw new DomainException("Resmin boyutları çok büyük.");
            }

            // Kısa kenar çıktıya yetecek kadar küçültülerek çözülür (destekleyen
            // biçimlerde). Codec desteklemediği oranı en yakın desteklenene yuvarlar.
            var scale = Math.Min(1f, (float)OutputSize / Math.Min(declared.Width, declared.Height));
            var decodeSize = codec.GetScaledDimensions(scale);
            if ((long)decodeSize.Width * decodeSize.Height > MaxDecodedPixels)
            {
                throw new DomainException("Resmin boyutları çok büyük.");
            }

            var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var decoded = new SKBitmap(decodeInfo);
            var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
            // IncompleteInput: kesik ama kısmen okunabilen dosya; okunan kısım kullanılır.
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            {
                throw new DomainException("Resim çözülemedi.", StatusCodes.Status415UnsupportedMediaType);
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
            // JPEG'de saydamlık yok; saydam PNG'ler beyaz zemine oturur.
            canvas.Clear(SKColors.White);
            using var image = SKImage.FromBitmap(source);
            canvas.DrawImage(image, src, dst, new SKSamplingOptions(SKCubicResampler.Mitchell));

            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            return encoded.ToArray();
        }

        // EXIF yönü (telefon kameraları pikselleri döndürmeden yalnızca etiketler).
        // Her yön, kaynak noktası (x, y) → hedef noktası eşlemesiyle verilir
        // (w, h: kaynak boyutu):
        //   TopRight (2)    yatay ayna      (w-x, y)
        //   BottomRight (3) 180°            (w-x, h-y)
        //   BottomLeft (4)  dikey ayna      (x, h-y)
        //   LeftTop (5)     transpoze       (y, x)
        //   RightTop (6)    saat yönü 90°   (h-y, x)
        //   RightBottom (7) transvers       (h-y, w-x)
        //   LeftBottom (8)  saat tersi 90°  (y, w-x)
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
                _ => null, // TopLeft: olduğu gibi
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
            // 90°/ayna dönüşümleri piksel tam; en yakın komşu kayıpsızdır.
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            return result;
        }

        private static SKMatrix Affine(float sx, float kx, float tx, float ky, float sy, float ty) =>
            new(sx, kx, tx, ky, sy, ty, 0, 0, 1);
    }
}
