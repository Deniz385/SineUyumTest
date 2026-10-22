using System.Text;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Services.Media;
using SkiaSharp;
using static SineUyum.Api.Tests.TestImages;

namespace SineUyum.Api.Tests;

// B8: profile photo processing — validation, EXIF orientation, metadata
// removal, memory safety.
public class AvatarImageProcessorTests
{
    private static SKBitmap Decode(byte[] jpeg) => SKBitmap.Decode(jpeg);

    // The expected layout was derived independently of the EXIF definition:
    // "where do row 0 / column 0 land in the displayed image". Order: top left,
    // top right, bottom left, bottom right.
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
    public void AllExifOrientations_AreAppliedCorrectly(SKEncodedOrigin origin, string tl, string tr, string bl, string br)
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
    public void Output_Is512SquareJpeg_CroppedFromTheCenter()
    {
        // 900x300 landscape: left half red, right half blue → after cropping the
        // central 300x300 the left half must stay red and the right half blue.
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
    public void ExifOrientationIsApplied_AndAllMetadataRemoved()
    {
        const string secret = "SECRET-LOCATION-41.0082N-28.9784E";
        // Left red / right blue, orientation 6 (90 degrees clockwise) → in the
        // displayed image the top is red and the bottom blue.
        var input = WithExif(Jpeg(600, 400), orientation: 6, description: secret);
        Assert.Contains(secret, Encoding.ASCII.GetString(input)); // the input really contains it

        var output = AvatarImageProcessor.Process(input);

        var text = Encoding.ASCII.GetString(output);
        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain("Exif", text);

        using var bitmap = Decode(output);
        Assert.True(Near(bitmap.GetPixel(256, 100), Red), "the top should be red (orientation not applied)");
        Assert.True(Near(bitmap.GetPixel(256, 412), Blue), "the bottom should be blue");
    }

    [Fact]
    public void TransparentPng_LandsOnWhiteBackground()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(300, 300, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);

        using var result = Decode(AvatarImageProcessor.Process(Encode(bitmap, SKEncodedImageFormat.Png)));

        Assert.True(Near(result.GetPixel(256, 256), SKColors.White, 5));
    }

    [Fact]
    public void Webp_IsAccepted()
    {
        using var source = LeftRedRightBlue(400, 400);
        var output = AvatarImageProcessor.Process(Encode(source, SKEncodedImageFormat.Webp));
        Assert.Equal(512, Decode(output).Width);
    }

    // Large phone photos (JPEG) must be accepted because they are decoded scaled.
    [Fact]
    public void Large28MpJpeg_IsDecodedScaledAndAccepted()
    {
        var output = AvatarImageProcessor.Process(Jpeg(7000, 4000));
        Assert.Equal(512, Decode(output).Width);
    }

    [Fact]
    public void BombDeclaringItsSize_IsRejectedWithoutDecodingPixels()
    {
        var bomb = PngHeaderOnly(50_000, 50_000); // 2.5 billion pixels, ~10 GB
        Assert.True(bomb.Length < 200);

        var ex = Assert.Throws<DomainException>(() => AvatarImageProcessor.Process(bomb));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("profile.photoDimensionsTooLarge", ex.MessageKey);
    }

    // PNG cannot be decoded scaled: even if the declared size is below the
    // limit, it is rejected when the size to decode exceeds 25 MP.
    [Fact]
    public void LargePngThatCannotBeScaled_IsRejected()
    {
        var ex = Assert.Throws<DomainException>(() => AvatarImageProcessor.Process(PngHeaderOnly(6000, 5000)));
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("gif")]
    [InlineData("html")]
    public void NotAnImageOrUnsupported_415(string kind)
    {
        byte[] input = kind switch
        {
            "text" => Encoding.UTF8.GetBytes("this is not an image"),
            "html" => Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"),
            _ => Gif1x1,
        };

        var ex = Assert.Throws<DomainException>(() => AvatarImageProcessor.Process(input));
        Assert.Equal(415, ex.StatusCode);
    }

    // The smallest valid GIF (1x1); GIF is not supported (animation, old format).
    private static readonly byte[] Gif1x1 = Convert.FromBase64String(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
}
