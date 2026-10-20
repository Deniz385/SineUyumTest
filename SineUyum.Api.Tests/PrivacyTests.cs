using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// G12: profil görünürlüğü, mesaj izni, aramada görünmeme ve engelleme. Her
// ayar bağımsız açılıp kapatılabilmeli ve kurallar sunucuda uygulanmalı.
public class PrivacyTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public PrivacyTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id, string UserName);

    private async Task<Session> RegisterAsync(string prefix = "gz")
    {
        var username = prefix + "_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Sifre123" });
        return await LoginAsync(username, "Sifre123");
    }

    private async Task<Session> LoginAsync(string username, string password)
    {
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return new Session(
            root.GetProperty("token").GetString()!,
            root.GetProperty("user").GetProperty("id").GetString()!,
            root.GetProperty("user").GetProperty("userName").GetString()!);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session s, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        if (body != null) req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private async Task<JsonElement> JsonAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private async Task SetPrivacyAsync(Session s, string visibility = "Public", string messages = "Everyone", bool hidden = false)
    {
        var res = await SendAsync(HttpMethod.Put, "/api/v1/account/privacy", s,
            new { profileVisibility = visibility, messagePermission = messages, hiddenFromSearch = hidden });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private Task<HttpResponseMessage> MessageAsync(Session from, Session to) =>
        SendAsync(HttpMethod.Post, "/api/v1/messages", from, new { recipientId = to.Id, content = "merhaba" });

    private Task<HttpResponseMessage> FollowAsync(Session from, Session to) =>
        SendAsync(HttpMethod.Post, $"/api/v1/follow/{to.Id}", from);

    private async Task<bool> FindsInSearchAsync(Session searcher, Session target)
    {
        var res = await SendAsync(HttpMethod.Get, $"/api/v1/account/search?query={target.UserName}", searcher);
        res.EnsureSuccessStatusCode();
        return (await JsonAsync(res)).EnumerateArray().Any(u => u.GetProperty("id").GetString() == target.Id);
    }

    private async Task<string?> ProblemTitleAsync(HttpResponseMessage res) =>
        (await JsonAsync(res)).GetProperty("title").GetString();

    // Puanlar TMDb'siz, doğrudan veritabanına yazılır.
    private async Task SeedRatingAsync(string userId, int movieId, int rating)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Movies.AnyAsync(m => m.Id == movieId))
        {
            db.Movies.Add(new Movie { Id = movieId, Title = $"Film {movieId}" });
        }
        db.UserRatings.Add(new UserRating { UserId = userId, MovieId = movieId, Rating = rating });
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> ProfileAsync(Session viewer, Session owner)
    {
        var res = await SendAsync(HttpMethod.Get, $"/api/v1/profile/{owner.Id}", viewer);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await JsonAsync(res);
    }

    [Fact]
    public async Task Varsayilanlar_MevcutDavranisla_Ayni()
    {
        var me = await RegisterAsync();
        var res = await SendAsync(HttpMethod.Get, "/api/v1/account/privacy", me);
        var json = await JsonAsync(res);

        Assert.Equal("Public", json.GetProperty("profileVisibility").GetString());
        Assert.Equal("Everyone", json.GetProperty("messagePermission").GetString());
        Assert.False(json.GetProperty("hiddenFromSearch").GetBoolean());
    }

    [Fact]
    public async Task Ayarlar_KaydedilirVe_GeriOkunur()
    {
        var me = await RegisterAsync();
        await SetPrivacyAsync(me, "Following", "Nobody", hidden: true);

        var json = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/account/privacy", me));
        Assert.Equal("Following", json.GetProperty("profileVisibility").GetString());
        Assert.Equal("Nobody", json.GetProperty("messagePermission").GetString());
        Assert.True(json.GetProperty("hiddenFromSearch").GetBoolean());
    }

    [Theory]
    [InlineData("{\"profileVisibility\":\"Herkes\",\"messagePermission\":\"Everyone\"}")]
    [InlineData("{\"profileVisibility\":7,\"messagePermission\":\"Everyone\"}")]
    [InlineData("{\"messagePermission\":\"Everyone\"}")]
    [InlineData("{\"profileVisibility\":\"Public\"}")]
    public async Task GecersizAyar_400(string body)
    {
        var me = await RegisterAsync();
        var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/account/privacy")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", me.Token);
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GizliProfil_AyrintilarSadeceSahibine()
    {
        var owner = await RegisterAsync();
        var viewer = await RegisterAsync();
        await SeedRatingAsync(owner.Id, 910001, 10);
        await SeedRatingAsync(viewer.Id, 910001, 9);

        await SetPrivacyAsync(owner, "Private");

        var seen = await ProfileAsync(viewer, owner);
        Assert.True(seen.GetProperty("isRestricted").GetBoolean());
        Assert.Equal(owner.UserName, seen.GetProperty("userName").GetString());
        Assert.Empty(seen.GetProperty("ratings").EnumerateArray());
        Assert.Equal(0, seen.GetProperty("statistics").GetProperty("totalRatings").GetInt32());

        // Puanlardan türeyen her şey de kapalı.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{owner.Id}", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{owner.Id}/followers", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{owner.Id}/following", viewer)).StatusCode);

        // Sahibi ve yönetici her şeyi görür.
        var own = await ProfileAsync(owner, owner);
        Assert.False(own.GetProperty("isRestricted").GetBoolean());
        Assert.Equal(1, own.GetProperty("statistics").GetProperty("totalRatings").GetInt32());

        var admin = await LoginAsync(TestWebAppFactory.AdminUserName, TestWebAppFactory.AdminPassword);
        Assert.False((await ProfileAsync(admin, owner)).GetProperty("isRestricted").GetBoolean());

        // Kapatınca yeniden açık.
        await SetPrivacyAsync(owner, "Public");
        var again = await ProfileAsync(viewer, owner);
        Assert.False(again.GetProperty("isRestricted").GetBoolean());
        Assert.Single(again.GetProperty("ratings").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{owner.Id}", viewer)).StatusCode);
    }

    [Fact]
    public async Task TakipEttiklerim_SahibinTakibiBelirler()
    {
        var owner = await RegisterAsync();
        var viewer = await RegisterAsync();
        await SetPrivacyAsync(owner, "Following");

        Assert.True((await ProfileAsync(viewer, owner)).GetProperty("isRestricted").GetBoolean());

        // Görüntüleyenin sahibi takip etmesi yetmez (takip onaysız).
        Assert.Equal(HttpStatusCode.OK, (await FollowAsync(viewer, owner)).StatusCode);
        Assert.True((await ProfileAsync(viewer, owner)).GetProperty("isRestricted").GetBoolean());

        // Sahip görüntüleyeni takip edince açılır.
        Assert.Equal(HttpStatusCode.OK, (await FollowAsync(owner, viewer)).StatusCode);
        Assert.False((await ProfileAsync(viewer, owner)).GetProperty("isRestricted").GetBoolean());
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{owner.Id}/followers", viewer)).StatusCode);
    }

    [Fact]
    public async Task MesajIzni_HerSecenek()
    {
        var recipient = await RegisterAsync();
        var sender = await RegisterAsync();

        await SetPrivacyAsync(recipient, messages: "Nobody");
        var nobody = await MessageAsync(sender, recipient);
        Assert.Equal(HttpStatusCode.Forbidden, nobody.StatusCode);
        Assert.Equal("Bu kullanıcı mesaj kabul etmiyor.", await ProblemTitleAsync(nobody));

        await SetPrivacyAsync(recipient, messages: "Following");
        var notFollowed = await MessageAsync(sender, recipient);
        Assert.Equal(HttpStatusCode.Forbidden, notFollowed.StatusCode);
        Assert.Equal("Bu kullanıcı yalnızca takip ettiği kişilerden mesaj kabul ediyor.",
            await ProblemTitleAsync(notFollowed));

        // Göndericinin alıcıyı takip etmesi yetmez; alıcı göndericiyi takip etmeli.
        await FollowAsync(sender, recipient);
        Assert.Equal(HttpStatusCode.Forbidden, (await MessageAsync(sender, recipient)).StatusCode);
        await FollowAsync(recipient, sender);
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(sender, recipient)).StatusCode);

        // Kısıt yalnızca alıcıya gelen mesajlar için: alıcı istediğine yazabilir.
        await SetPrivacyAsync(recipient, messages: "Nobody");
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(recipient, sender)).StatusCode);

        await SetPrivacyAsync(recipient, messages: "Everyone");
        var stranger = await RegisterAsync();
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(stranger, recipient)).StatusCode);
    }

    [Fact]
    public async Task AramadaGorunmeme_AcilipKapanir()
    {
        var target = await RegisterAsync("arama");
        var searcher = await RegisterAsync();
        Assert.True(await FindsInSearchAsync(searcher, target));

        await SetPrivacyAsync(target, hidden: true);
        Assert.False(await FindsInSearchAsync(searcher, target));
        // Yalnızca aramadan gizler; profil bağlantısı çalışmaya devam eder.
        Assert.False((await ProfileAsync(searcher, target)).GetProperty("isRestricted").GetBoolean());

        await SetPrivacyAsync(target, hidden: false);
        Assert.True(await FindsInSearchAsync(searcher, target));
    }

    [Fact]
    public async Task Engel_IkiYondeEtkili_KaldirilincaBiter()
    {
        var a = await RegisterAsync("engel");
        var b = await RegisterAsync("engel");
        await FollowAsync(a, b);
        await FollowAsync(b, a);
        await MessageAsync(b, a);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{b.Id}", a)).StatusCode);
        // Tekrarlanabilir.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{b.Id}", a)).StatusCode);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/blocks", a));
        Assert.Equal(b.Id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetString());
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/blocks", b))).EnumerateArray());

        // Engellenen, engelleyeni hiç göremez.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/profile/{a.Id}", b)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{a.Id}/status", b)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{a.Id}", b)).StatusCode);
        // Engelleyen yalnızca kimliği görür (engeli kaldırabilsin).
        var blockedView = await ProfileAsync(a, b);
        Assert.True(blockedView.GetProperty("isRestricted").GetBoolean());
        Assert.True(blockedView.GetProperty("isBlockedByMe").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{b.Id}", a)).StatusCode);

        // Mesaj, takip ve arama iki yönde kapalı; takipler silinmiş.
        Assert.Equal(HttpStatusCode.Forbidden, (await MessageAsync(a, b)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MessageAsync(b, a)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await FollowAsync(a, b)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await FollowAsync(b, a)).StatusCode);
        Assert.False(await FindsInSearchAsync(a, b));
        Assert.False(await FindsInSearchAsync(b, a));
        var status = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/follow/{b.Id}/status", a));
        Assert.False(status.GetProperty("isFollowing").GetBoolean());
        Assert.Equal(0, status.GetProperty("followerCount").GetInt32());
        Assert.Equal(0, status.GetProperty("followingCount").GetInt32());

        // Geçmiş sohbet okunabilir kalır.
        var thread = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/messages/thread/{b.Id}", a));
        Assert.Single(thread.EnumerateArray());

        // Engelleyen olmayan taraf engeli kaldıramaz; sahibi kaldırır.
        await SendAsync(HttpMethod.Delete, $"/api/v1/blocks/{a.Id}", b);
        Assert.Equal(HttpStatusCode.Forbidden, (await MessageAsync(b, a)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Delete, $"/api/v1/blocks/{b.Id}", a)).StatusCode);
        Assert.False((await ProfileAsync(b, a)).GetProperty("isRestricted").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(b, a)).StatusCode);
        Assert.True(await FindsInSearchAsync(a, b));
        // Takipler kendiliğinden geri gelmez.
        var after = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/follow/{b.Id}/status", a));
        Assert.False(after.GetProperty("isFollowing").GetBoolean());
    }

    [Fact]
    public async Task Engel_GecersizHedefler()
    {
        var me = await RegisterAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{me.Id}", me)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, "/api/v1/blocks/olmayan-kullanici", me)).StatusCode);
        // Olmayan kullanıcıyı takip etmek önceden FK hatasıyla 500 veriyordu.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, "/api/v1/follow/olmayan-kullanici", me)).StatusCode);
    }

    [Fact]
    public async Task Eslestirme_EngelliKisileriAyniGrubaKoymaz()
    {
        // a ve b birebir aynı zevkte (en uyumlu çift), c ve d de öyle; a, b'yi
        // engelliyor. Başlangıç kişisi rastgele: engel dikkate alınmasaydı her
        // etkinlikte 1/2 ihtimalle a+b aynı gruba düşerdi.
        var a = await RegisterAsync("es");
        var b = await RegisterAsync("es");
        var c = await RegisterAsync("es");
        var d = await RegisterAsync("es");
        var outsider = await RegisterAsync("es");
        foreach (var u in new[] { a, b })
        {
            await SeedRatingAsync(u.Id, 920001, 9);
            await SeedRatingAsync(u.Id, 920003, 10);
            await SeedRatingAsync(u.Id, 920004, 1);
        }
        foreach (var u in new[] { c, d })
        {
            await SeedRatingAsync(u.Id, 920001, 9);
            await SeedRatingAsync(u.Id, 920003, 1);
            await SeedRatingAsync(u.Id, 920004, 10);
        }
        // Öneriler TMDb'siz üretilsin: grup dışından benzer bir kullanıcı.
        await SeedRatingAsync(outsider.Id, 920001, 9);
        await SeedRatingAsync(outsider.Id, 920002, 10);

        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{b.Id}", a);

        for (var i = 0; i < 12; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ev = new CinemaEvent { EventDate = DateTime.UtcNow.AddDays(3), LocationName = $"Salon {i}", GroupSize = 2 };
            db.CinemaEvents.Add(ev);
            await db.SaveChangesAsync();
            foreach (var u in new[] { a, b, c, d })
            {
                db.EventParticipants.Add(new EventParticipant { CinemaEventId = ev.Id, UserId = u.Id });
            }
            await db.SaveChangesAsync();

            await scope.ServiceProvider.GetRequiredService<MatchingService>().CreateGroupsForEventAsync(ev.Id);

            var groups = await db.EventGroups
                .Where(g => g.CinemaEventId == ev.Id)
                .Select(g => g.Members.Select(m => m.UserId).ToList())
                .ToListAsync();
            Assert.DoesNotContain(groups, g => g.Contains(a.Id) && g.Contains(b.Id));
            // Engel kimseyi gruptan etmiyor: a ve b ayrı gruplarda yer bulur.
            Assert.Equal(2, groups.Count);
        }
    }
}
