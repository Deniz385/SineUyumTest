using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SineUyum.Api.Tests;

// M3: API sözleşmesini sabitler. Gerçekçi bir senaryo (kayıt, puanlama, takip,
// liste, mesaj, bildirim, etkinlik eşleştirme...) tüm JSON uçlarından geçer;
// her yanıtın ŞEKLİ (durum kodu + alan adları + değer türleri) altın dosyayla
// (Contracts/response-shapes.txt) karşılaştırılır. Web ve mobil istemcinin
// okuduğu bir alan yanlışlıkla kaldırılır/yeniden adlandırılırsa CI kırılır.
//
// Bilinçli bir sözleşme değişikliğinden sonra altın dosyayı yeniden üretmek için:
//   UPDATE_CONTRACTS=1 dotnet test --filter ResponseContractTests
// ve diff'i gözden geçirip commit'leyin.
//
// TMDb'yi olduğu gibi aktaran uçlar (movies/*, compatibility/.../recommendations)
// TMDb'nin şemasıdır, bizim sözleşmemiz değildir; bu yüzden kapsam dışıdır.
public class ResponseContractTests : IClassFixture<ResponseContractTests.Factory>
{
    private readonly Factory _factory;

    public ResponseContractTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private sealed class FakeTmdb : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            string body;
            if (path.EndsWith("/movie/popular"))
            {
                body = "{\"results\":[" +
                       "{\"id\":900,\"title\":\"Populer 1\",\"poster_path\":\"/p1.jpg\"}," +
                       "{\"id\":901,\"title\":\"Populer 2\",\"poster_path\":\"/p2.jpg\"}]}";
            }
            else
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                body = $"{{\"id\":{id},\"title\":\"Film {id}\",\"poster_path\":\"/f{id}.jpg\"}}";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    public class Factory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["TMDb:ApiKey"] = "test-tmdb-key" }));
            builder.ConfigureServices(services =>
                services.AddHttpClient("TMDb").ConfigurePrimaryHttpMessageHandler(() => new FakeTmdb()));
        }
    }

    // --- Şekil çıkarımı ---------------------------------------------------

    private static string Kind(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "array",
        _ => "object",
    };

    private static void Walk(JsonElement e, string path, SortedSet<string> lines)
    {
        lines.Add($"{(path == "" ? "$" : path)}: {Kind(e)}");
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
                Walk(p.Value, path == "" ? p.Name : $"{path}.{p.Name}", lines);
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
                Walk(item, $"{path}[]", lines);
        }
    }

    private readonly StringBuilder _report = new();
    private readonly HttpClient _client;

    private async Task<JsonElement> Call(string name, HttpMethod method, string url, string? token = null, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // HttpContent verilirse (ör. multipart) olduğu gibi, yoksa JSON gönderilir.
        if (body is HttpContent content) req.Content = content;
        else if (body != null) req.Content = JsonContent.Create(body);

        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();

        // Kimlikler her çalıştırmada değişir; şablonu sabit tutmak için maskele.
        var route = System.Text.RegularExpressions.Regex.Replace(url.Split('?')[0],
            @"/([0-9a-f]{8}-[0-9a-f-]{27}|\d+)(?=/|$)", "/{id}");
        _report.AppendLine($"## {name}  {method} {route} -> {(int)res.StatusCode}");
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(text) &&
            res.Content.Headers.ContentType?.MediaType?.Contains("json") == true)
        {
            root = JsonDocument.Parse(text).RootElement.Clone();
            var lines = new SortedSet<string>(StringComparer.Ordinal);
            Walk(root, "", lines);
            foreach (var l in lines) _report.AppendLine("  " + l);
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            _report.AppendLine("  $: text");
        }
        _report.AppendLine();

        Assert.True(res.IsSuccessStatusCode, $"{name}: beklenmeyen durum {(int)res.StatusCode}: {text}");
        return root;
    }

    private Task<JsonElement> Get(string name, string url, string token) => Call(name, HttpMethod.Get, url, token);
    private Task<JsonElement> Post(string name, string url, string? token, object? body = null) => Call(name, HttpMethod.Post, url, token, body);

    private const string GoldenFile = "response-shapes.txt";

    // Karşılaştırma, csproj'un çıktıya kopyaladığı dosyadan okunur (derleme
    // yolu eşlemesinden etkilenmez). Yeniden üretim ise kaynak ağacına yazar.
    private static string GoldenOutputPath() =>
        Path.Combine(AppContext.BaseDirectory, "Contracts", GoldenFile);

    private static string GoldenSourcePath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "Contracts", GoldenFile);

    // --- Senaryo ------------------------------------------------------------

    [Fact]
    public async Task TumUclarinYanitSekli_AltinDosyaylaAyni()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string aName = "ca" + suffix, bName = "cb" + suffix;

        await Call("app.config", HttpMethod.Get, "/api/v1/app/config");

        await Post("account.register", "/api/v1/account/register", null,
            new { username = aName, email = $"{aName}@test.com", password = "Sifre123" });
        await Post("account.register(b)", "/api/v1/account/register", null,
            new { username = bName, email = $"{bName}@test.com", password = "Sifre123" });

        var aLogin = await Post("account.login", "/api/v1/account/login", null, new { username = aName, password = "Sifre123" });
        var bLogin = await Post("account.login(b)", "/api/v1/account/login", null, new { username = bName, password = "Sifre123" });
        var a = aLogin.GetProperty("token").GetString()!;
        var b = bLogin.GetProperty("token").GetString()!;
        var aId = aLogin.GetProperty("user").GetProperty("id").GetString()!;
        var bId = bLogin.GetProperty("user").GetProperty("id").GetString()!;

        var refreshed = await Post("account.refresh", "/api/v1/account/refresh", null,
            new { refreshToken = aLogin.GetProperty("refreshToken").GetString() });
        a = refreshed.GetProperty("token").GetString()!;
        await Get("account.me", "/api/v1/account/me", a);
        await Post("devices.register", "/api/v1/devices", a, new { token = "fcm-sozlesme-" + suffix, platform = "android" });

        // Puanlama (film TMDb'den çekilir) ve ortak film.
        await Post("ratings.add", "/api/v1/ratings", a, new { movieId = 550, rating = 9 });
        await Post("ratings.add(b)", "/api/v1/ratings", b, new { movieId = 550, rating = 9 });
        await Post("ratings.addmovie", "/api/v1/ratings/addmovie", a, new { id = 551 });

        await Get("account.search", $"/api/v1/account/search?query=cb{suffix}", a);
        await Get("compatibility.get", $"/api/v1/compatibility/{bId}", a);

        // Takip.
        await Post("follow.follow", $"/api/v1/follow/{bId}", a);
        await Get("follow.status", $"/api/v1/follow/{bId}/status", a);
        await Get("follow.followers", $"/api/v1/follow/{bId}/followers", a);
        await Get("follow.following", $"/api/v1/follow/{aId}/following", a);

        // İzleme listesi.
        var list = await Post("watchlist.create", "/api/v1/watchlist", a, new { name = "Liste", description = "Aciklama" });
        var listId = list.GetProperty("id").GetInt32();
        await Get("watchlist.list", "/api/v1/watchlist", a);
        await Post("watchlist.addMovie", $"/api/v1/watchlist/{listId}/movies", a, new { movieId = 552, title = "Istemci Basligi", posterPath = "/istemci.jpg" });
        await Get("watchlist.details", $"/api/v1/watchlist/{listId}", a);
        await Get("watchlist.public", $"/api/v1/watchlist/public/{listId}", b);
        await Call("watchlist.update", HttpMethod.Put, $"/api/v1/watchlist/{listId}", a, new { name = "Liste 2", description = "x" });

        // Mesajlar: düz metin, film önerisi, liste paylaşımı.
        await Post("messages.create", "/api/v1/messages", a, new { recipientId = bId, content = "selam" });
        await Post("messages.create(movie)", "/api/v1/messages", a, new { recipientId = bId, movieId = 550 });
        await Post("messages.create(list)", "/api/v1/messages", a, new { recipientId = bId, watchlistId = listId });
        await Get("messages.thread", $"/api/v1/messages/thread/{aId}", b);
        await Get("messages.conversations", "/api/v1/messages", b);
        await Post("messages.markThreadRead", $"/api/v1/messages/thread/{aId}/mark-as-read", b);

        // Bildirimler.
        var notifications = await Get("notification.list", "/api/v1/notification", b);
        var notificationId = notifications[0].GetProperty("id").GetInt32();
        await Post("notification.markOne", $"/api/v1/notification/{notificationId}/mark-as-read", b);
        await Post("notification.markAll", "/api/v1/notification/mark-all-as-read", b);

        // Profil.
        await Get("profile.get", $"/api/v1/profile/{aId}", b);
        await Call("profile.update", HttpMethod.Put, "/api/v1/profile", a, new { bio = "bio", profileImageUrl = (string?)null });

        // Profil fotoğrafı: yükle (sonraki yanıtlarda profileImageUrl dolu
        // görünür), bir kez de kaldırıp yeniden yükle.
        MultipartFormDataContent Photo()
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(TestImages.Jpeg(300, 300)), "file", "foto.jpg");
            return form;
        }
        await Post("profile.photo.upload", "/api/v1/profile/photo", a, Photo());
        await Call("profile.photo.delete", HttpMethod.Delete, "/api/v1/profile/photo", a);
        await Post("profile.photo.upload(2)", "/api/v1/profile/photo", a, Photo());

        // Etkinlik yaşam döngüsü: NONE -> AVAILABLE -> PENDING -> MATCHED.
        var adminLogin = await Post("account.login(admin)", "/api/v1/account/login", null,
            new { username = TestWebAppFactory.AdminUserName, password = TestWebAppFactory.AdminPassword });
        var admin = adminLogin.GetProperty("token").GetString()!;

        // Sınıf kendi fabrikasını (ve veritabanını) kullandığından etkinlik yok.
        await Get("event.myStatus(NONE)", "/api/v1/event/my-status", a);
        var ev = await Post("event.create", "/api/v1/event", admin,
            new { eventDate = DateTime.UtcNow.AddDays(7), locationName = "Sinema", address = "Adres", groupSize = 2 });
        var eventId = ev.GetProperty("id").GetInt32();
        await Get("event.list", "/api/v1/event", admin);
        await Get("event.myStatus(AVAILABLE)", "/api/v1/event/my-status", a);
        // Etkinlik ücretli özellik (G4): katılımdan önce abonelik gerekir.
        await Get("subscription.status(none)", "/api/v1/subscription", a);
        await Post("admin.grantSubscription", $"/api/v1/admin/users/{aId}/subscription", admin, new { days = 30 });
        await Post("admin.grantSubscription(b)", $"/api/v1/admin/users/{bId}/subscription", admin, new { days = 30 });
        await Get("subscription.status(active)", "/api/v1/subscription", a);
        await Post("event.join", $"/api/v1/event/{eventId}/join", a);
        await Post("event.join(b)", $"/api/v1/event/{eventId}/join", b);
        await Get("event.myStatus(PENDING)", "/api/v1/event/my-status", a);
        await Post("admin.createGroups", $"/api/v1/admin/events/{eventId}/create-groups", admin);
        var matched = await Get("event.myStatus(MATCHED)", "/api/v1/event/my-status", a);
        var groupId = matched.GetProperty("group").GetProperty("id").GetInt32();
        var movieToVote = matched.GetProperty("suggestedMovies")[0].GetProperty("id").GetInt32();
        await Post("event.vote", "/api/v1/event/vote", a, new { groupId, movieId = movieToVote });
        await Get("event.myStatus(MATCHED,voted)", "/api/v1/event/my-status", a);
        await Call("event.update", HttpMethod.Put, $"/api/v1/event/{eventId}", admin,
            new { eventDate = DateTime.UtcNow.AddDays(8), locationName = "Sinema 2", address = "Adres", groupSize = 2 });

        var ev2 = await Post("event.create(2)", "/api/v1/event", admin,
            new { eventDate = DateTime.UtcNow.AddDays(30), locationName = "Silinecek", address = (string?)null, groupSize = 4 });
        await Call("event.delete", HttpMethod.Delete, $"/api/v1/event/{ev2.GetProperty("id").GetInt32()}", admin);

        // Temizlik uçları.
        await Call("watchlist.removeMovie", HttpMethod.Delete, $"/api/v1/watchlist/{listId}/movies/552", a);
        await Call("watchlist.delete", HttpMethod.Delete, $"/api/v1/watchlist/{listId}", a);
        await Call("follow.unfollow", HttpMethod.Delete, $"/api/v1/follow/{bId}", a);
        var changed = await Post("account.changePassword", "/api/v1/account/change-password", a,
            new { currentPassword = "Sifre123", newPassword = "YeniSifre123" });
        await Post("devices.unregister", "/api/v1/devices/unregister", null, new { token = "fcm-sozlesme-" + suffix });
        await Post("account.logout", "/api/v1/account/logout", changed.GetProperty("token").GetString()!,
            new { refreshToken = changed.GetProperty("refreshToken").GetString() });

        var actual = _report.ToString().Replace("\r\n", "\n");

        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACTS") == "1")
        {
            var source = GoldenSourcePath();
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, actual);
            return;
        }

        var golden = GoldenOutputPath();
        Assert.True(File.Exists(golden), $"Altın dosya yok: {golden}. UPDATE_CONTRACTS=1 ile üretin.");
        var expected = File.ReadAllText(golden).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }
}
