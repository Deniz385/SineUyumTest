using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SkiaSharp;
using static SineUyum.Api.Tests.PushTestSupport;
using static SineUyum.Api.Tests.TestImages;

namespace SineUyum.Api.Tests;

// B8: profil fotoğrafı yükleme, sunma, değiştirme, silme.
public class ProfilePhotoTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ProfilePhotoTests(TestWebAppFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string? token, byte[] bytes,
        string fileName = "foto.jpg", string contentType = "image/jpeg", string field = "file")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, field, fileName);
        return await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/profile/photo")
        {
            Content = form,
            Headers = { Authorization = token == null ? null : new AuthenticationHeaderValue("Bearer", token) },
        });
    }

    private static async Task<string?> UrlFromAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var value = doc.RootElement.GetProperty("profileImageUrl");
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private async Task<string?> ProfileImageAsync(HttpClient client, Session s)
    {
        var res = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/account/me", s.Token));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var value = doc.RootElement.GetProperty("profileImageUrl");
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private int FilesFor(Session s) =>
        Directory.Exists(Path.Combine(_factory.MediaRoot, "avatars", s.UserId))
            ? Directory.GetFiles(Path.Combine(_factory.MediaRoot, "avatars", s.UserId)).Length
            : 0;

    [Fact]
    public async Task Yukleme_YenidenKodlanmisJpegSunulur()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "ph_");

        var res = await UploadAsync(client, me.Token, WithExif(Jpeg(800, 600), 1, "GIZLI-ETIKET"), "tatil.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var url = await UrlFromAsync(res);

        // Sunucu seçer: istemcinin dosya adı/uzantısı/türü kullanılmaz.
        Assert.Matches($"^/media/avatars/{me.UserId}/[0-9a-f]{{32}}\\.jpg$", url);
        Assert.Equal(url, await ProfileImageAsync(client, me));

        var served = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/jpeg", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("immutable", served.Headers.CacheControl?.ToString());

        var bytes = await served.Content.ReadAsByteArrayAsync();
        Assert.DoesNotContain("GIZLI-ETIKET", System.Text.Encoding.ASCII.GetString(bytes));
        using var bitmap = SKBitmap.Decode(bytes);
        Assert.Equal(512, bitmap.Width);
        Assert.Equal(512, bitmap.Height);
    }

    [Fact]
    public async Task YenidenYukleme_EskiDosyayiSiler()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "ph_");

        var first = await UrlFromAsync(await UploadAsync(client, me.Token, Jpeg(300, 300)));
        var second = await UrlFromAsync(await UploadAsync(client, me.Token, Jpeg(300, 300)));

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(second)).StatusCode);
        Assert.Equal(1, FilesFor(me));
    }

    [Fact]
    public async Task Silme_DosyayiVeAlaniTemizler()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "ph_");
        var url = await UrlFromAsync(await UploadAsync(client, me.Token, Jpeg(300, 300)));

        var res = await client.SendAsync(Authed(HttpMethod.Delete, "/api/v1/profile/photo", me.Token));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Null(await UrlFromAsync(res));
        Assert.Null(await ProfileImageAsync(client, me));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(0, FilesFor(me));
    }

    [Fact]
    public async Task ProfilGuncelleme_YeniHariciUrlReddedilir_MevcutDegerKabul()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "ph_");
        var url = await UrlFromAsync(await UploadAsync(client, me.Token, Jpeg(300, 300)));

        // Başka sunucudaki resim (izleme pikseli) — reddedilir.
        var external = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/profile", me.Token,
            new { bio = "x", profileImageUrl = "https://kotu.example/piksel.png" }));
        Assert.Equal(HttpStatusCode.BadRequest, external.StatusCode);

        // Başka bir kullanıcının dosyası da gösterilemez.
        var other = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/profile", me.Token,
            new { bio = "x", profileImageUrl = "/media/avatars/baskasi/abc.jpg" }));
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);

        // İstemciler bio güncellerken mevcut değeri geri yollar: kabul edilir.
        var unchanged = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/profile", me.Token,
            new { bio = "yeni bio", profileImageUrl = url }));
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Equal(url, await ProfileImageAsync(client, me));

        // Boş = kaldır (dosya da silinir).
        var cleared = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/profile", me.Token,
            new { bio = "yeni bio", profileImageUrl = "" }));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null(await ProfileImageAsync(client, me));
        Assert.Equal(0, FilesFor(me));
    }

    [Fact]
    public async Task ResimOlmayanDosya_415_HicbirSeyKaydedilmez()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "ph_");

        var res = await UploadAsync(client, me.Token,
            System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "foto.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, res.StatusCode);
        Assert.Null(await ProfileImageAsync(client, me));
        Assert.Equal(0, FilesFor(me));
    }

    [Fact]
    public async Task CokBuyukDosya_413()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "ph_");

        var res = await UploadAsync(client, me.Token, new byte[10 * 1024 * 1024 + 1]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
    }

    [Fact]
    public async Task DosyaYok_400_KimlikYok_401()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "ph_");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(client, me.Token, Jpeg(10, 10), field: "baska")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await UploadAsync(client, null, Jpeg(10, 10))).StatusCode);
    }

    [Theory]
    [InlineData("/media/../appsettings.json")]
    [InlineData("/media/%2e%2e/appsettings.json")]
    [InlineData("/media/..%2fappsettings.json")]
    public async Task MedyaKlasoruDisinaCikilamaz(string path)
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync(path);
        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task MedyaKlasorundekiJpgDisiDosyaSunulmaz()
    {
        Directory.CreateDirectory(_factory.MediaRoot);
        await File.WriteAllTextAsync(Path.Combine(_factory.MediaRoot, "zararli.html"), "<script>alert(1)</script>");

        var res = await _factory.CreateClient().GetAsync("/media/zararli.html");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
