using System.Text;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Services.Media;
using SkiaSharp;
using static SineUyum.Api.Tests.TestImages;

namespace SineUyum.Api.Tests;

// B8: profil fotoğrafı işleme — doğrulama, EXIF yönü, üst veri temizliği,
// bellek güvenliği.
public class AvatarImageProcessorTests
{
    private static SKBitmap Decode(byte[] jpeg) => SKBitmap.Decode(jpeg);

    // Beklenen düzen EXIF tanımından bağımsız türetildi: "satır 0 / sütun 0
    // görüntülenen resimde nereye düşer". Sıra: sol üst, sağ üst, sol alt, sağ alt.
    public static IEnumerable<object[]> Orientations() => new[]
    {
        new object[] { SKEncodedOrigin.TopLeft,     "R", "G", "B", "Y" },
        new object[] { SKEncodedOrigin.TopRight,    "G", "R", "Y", "B" },
        new object[] { SKEncodedOrigin.BottomRight, "Y", "B", "G", "R" },
        new object[] { SKEncodedOrigin.BottomLeft,  "B", "Y", "R", "G" },
        new object[] { SKEncodedOrigin.LeftTop,     "R", "B", "G", "Y" },
        new object[] { SKEncodedOrigin.RightTop,    "B", "R", "Y", "G" },
        new object[] { SKEncodedOrigin.RightBottom, "Y", "G", "B", "R" },
        new object[] { SKEncodedOrigin.LeftBottom,  "G", "Y", "R", "B" },
    };

    private static SKColor Color(string c) => c switch
    {
        "R" => Red, "G" => Green, "B" => Blue, _ => Yellow,
    };

    [Theory]
    [MemberData(nameof(Orientations))]
    public void TumExifYonleri_DogruUygulanir(SKEncodedOrigin origin, string tl, string tr, string bl, string br)
    {
        using var source = Quadrants(200, 100);
        using var result = AvatarImageProcessor.ApplyOrigin(source, origin);

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        Assert.Equal(swap ? 100 : 200, result.Width);
        Assert.Equal(swap ? 200 : 100, result.Height);

        int w = result.Width, h = result.Height;
        Assert.Equal(Color(tl), result.GetPixel(w / 4, h / 4));
        Assert.Equal(Color(tr), result.GetPixel(3 * w / 4, h / 4));
        Assert.Equal(Color(bl), result.GetPixel(w / 4, 3 * h / 4));
        Assert.Equal(Color(br), result.GetPixel(3 * w / 4, 3 * h / 4));
    }

    [Fact]
    public void Cikti_512KareJpeg_OrtadanKirpilir()
    {
        // 900x300 yatay: sol yarı kırmızı, sağ yarı mavi → ortadan 300x300 kırpılınca
        // sol yarı kırmızı, sağ yarı mavi kalmalı.
        var output = AvatarImageProcessor.Process(Jpeg(900, 300));

        using var codec = SKCodec.Create(SKData.CreateCopy(output));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        using var bitmap = Decode(output);
        Assert.Equal(512, bitmap.Width);
        Assert.Equal(512, bitmap.Height);
        Assert.True(Near(bitmap.GetPixel(100, 256), Red));
        Assert.True(Near(bitmap.GetPixel(412, 256), Blue));
    }

    [Fact]
    public void ExifYonuUygulanir_VeTumUstVeriSilinir()
    {
        const string secret = "GIZLI-KONUM-41.0082N-28.9784E";
        // Sol kırmızı / sağ mavi, yön 6 (saat yönünde 90°) → görüntülenen
        // resimde üst kırmızı, alt mavi.
        var input = WithExif(Jpeg(600, 400), orientation: 6, description: secret);
        Assert.Contains(secret, Encoding.ASCII.GetString(input)); // girdi gerçekten içeriyor

        var output = AvatarImageProcessor.Process(input);

        var text = Encoding.ASCII.GetString(output);
        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain("Exif", text);

        using var bitmap = Decode(output);
        Assert.True(Near(bitmap.GetPixel(256, 100), Red), "üst kırmızı olmalı (yön uygulanmadı)");
        Assert.True(Near(bitmap.GetPixel(256, 412), Blue), "alt mavi olmalı");
    }

    [Fact]
    public void SaydamPng_BeyazZemineOturur()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(300, 300, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);

        using var result = Decode(AvatarImageProcessor.Process(Encode(bitmap, SKEncodedImageFormat.Png)));

        Assert.True(Near(result.GetPixel(256, 256), SKColors.White, 5));
    }

    [Fact]
    public void Webp_KabulEdilir()
    {
        using var source = LeftRedRightBlue(400, 400);
        var output = AvatarImageProcessor.Process(Encode(source, SKEncodedImageFormat.Webp));
        Assert.Equal(512, Decode(output).Width);
    }

    // Büyük telefon fotoğrafları (JPEG) ölçekli çözüldüğü için kabul edilmeli.
    [Fact]
    public void Buyuk28MpJpeg_OlcekliCozulupKabulEdilir()
    {
        var output = AvatarImageProcessor.Process(Jpeg(7000, 4000));
        Assert.Equal(512, Decode(output).Width);
    }

    [Fact]
    public void BoyutBildirenBomba_PikselCozulmedenReddedilir()
    {
        var bomb = PngHeaderOnly(50_000, 50_000); // 2.5 milyar piksel, ~10 GB
        Assert.True(bomb.Length < 200);

        var ex = Assert.Throws<DomainException>(() => AvatarImageProcessor.Process(bomb));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("boyut", ex.Message);
    }

    // PNG ölçekli çözülemez: bildirilen boyut sınırın altında olsa da
    // çözülecek boyut 25 MP'yi aşıyorsa reddedilir.
    [Fact]
    public void OlceklenemeyenBuyukPng_Reddedilir()
    {
        var ex = Assert.Throws<DomainException>(() => AvatarImageProcessor.Process(PngHeaderOnly(6000, 5000)));
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("metin")]
    [InlineData("gif")]
    [InlineData("html")]
    public void ResimOlmayanVeyaDesteklenmeyen_415(string kind)
    {
        byte[] input = kind switch
        {
            "metin" => Encoding.UTF8.GetBytes("bu bir resim değil"),
            "html" => Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"),
            _ => Gif1x1,
        };

        var ex = Assert.Throws<DomainException>(() => AvatarImageProcessor.Process(input));
        Assert.Equal(415, ex.StatusCode);
    }

    // En küçük geçerli GIF (1x1); GIF desteklenmez (animasyon, eski biçim).
    private static readonly byte[] Gif1x1 = Convert.FromBase64String(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
}
