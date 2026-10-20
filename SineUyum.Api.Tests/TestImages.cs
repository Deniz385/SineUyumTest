using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace SineUyum.Api.Tests;

// Testlerde kullanılan resimler kodla üretilir (bayt düzeyinde kesin içerik).
public static class TestImages
{
    public static readonly SKColor Red = new(255, 0, 0);
    public static readonly SKColor Green = new(0, 255, 0);
    public static readonly SKColor Blue = new(0, 0, 255);
    public static readonly SKColor Yellow = new(255, 255, 0);

    // Dört çeyreği farklı renkli resim: sol üst kırmızı, sağ üst yeşil,
    // sol alt mavi, sağ alt sarı. Yön dönüşümlerini ayırt etmek için.
    public static SKBitmap Quadrants(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        void Fill(SKColor c, float l, float t, float r, float b) { paint.Color = c; canvas.DrawRect(new SKRect(l, t, r, b), paint); }
        Fill(Red, 0, 0, width / 2f, height / 2f);
        Fill(Green, width / 2f, 0, width, height / 2f);
        Fill(Blue, 0, height / 2f, width / 2f, height);
        Fill(Yellow, width / 2f, height / 2f, width, height);
        return bitmap;
    }

    // Sol yarısı kırmızı, sağ yarısı mavi.
    public static SKBitmap LeftRedRightBlue(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = Red };
        canvas.DrawRect(new SKRect(0, 0, width / 2f, height), paint);
        paint.Color = Blue;
        canvas.DrawRect(new SKRect(width / 2f, 0, width, height), paint);
        return bitmap;
    }

    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 95)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    public static byte[] Jpeg(int width, int height) { using var b = LeftRedRightBlue(width, height); return Encode(b, SKEncodedImageFormat.Jpeg); }

    // Bir JPEG'in SOI'sinden hemen sonra EXIF (APP1) bölümü ekler: yön etiketi
    // ve gizli kalması gereken bir açıklama (konum bilgisinin yerine).
    public static byte[] WithExif(byte[] jpeg, ushort orientation, string description)
    {
        var desc = Encoding.ASCII.GetBytes(description + "\0");
        var tiff = new byte[8 + 2 + 2 * 12 + 4 + desc.Length];
        var span = tiff.AsSpan();
        "II"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 8);
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 2); // giriş sayısı
        var dataOffset = (uint)(8 + 2 + 2 * 12 + 4);
        // 0x010E ImageDescription, ASCII
        WriteEntry(span[10..], 0x010E, 2, (uint)desc.Length, dataOffset);
        // 0x0112 Orientation, SHORT (değer alanın ilk 2 baytında)
        WriteEntry(span[22..], 0x0112, 3, 1, orientation);
        BinaryPrimitives.WriteUInt32LittleEndian(span[34..], 0); // sonraki IFD yok
        desc.CopyTo(span[(int)dataOffset..]);

        var payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF; segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(2 + payload.Length));
        payload.CopyTo(segment, 4);

        return jpeg.Take(2).Concat(segment).Concat(jpeg.Skip(2)).ToArray();

        static void WriteEntry(Span<byte> s, ushort tag, ushort type, uint count, uint value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(s, tag);
            BinaryPrimitives.WriteUInt16LittleEndian(s[2..], type);
            BinaryPrimitives.WriteUInt32LittleEndian(s[4..], count);
            BinaryPrimitives.WriteUInt32LittleEndian(s[8..], value);
        }
    }

    // Yalnızca başlığı geçerli bir PNG: istenen boyutu BİLDİRİR ama piksel
    // verisi neredeyse yoktur ("decompression bomb" taklidi). Gerçekten
    // çözülmeye çalışılsaydı gigabaytlarca bellek gerekirdi.
    public static byte[] PngHeaderOnly(int width, int height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; ihdr[9] = 6; // 8 bit RGBA
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", new byte[] { 0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01 });
        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(len, data.Length); s.Write(len);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes); s.Write(data);
            var crc = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeBytes.Concat(data).ToArray())); s.Write(crc);
        }
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    // JPEG kayıpsız değil; rengi toleransla karşılaştır.
    public static bool Near(SKColor actual, SKColor expected, int tolerance = 40) =>
        Math.Abs(actual.Red - expected.Red) <= tolerance &&
        Math.Abs(actual.Green - expected.Green) <= tolerance &&
        Math.Abs(actual.Blue - expected.Blue) <= tolerance;
}
