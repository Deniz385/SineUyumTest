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

// Haftalık zevk ikizi: en uyumlu, keşfedilmeye açık, takip edilmeyen ve daha
// önce önerilmemiş kişi; haftada bir ve bir kez bildirilir.
public class TasteTwinTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public TasteTwinTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id);

    private async Task<Session> RegisterAsync()
    {
        var username = "tw_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register", new { username, email = $"{username}@t.com", password = "Sifre123" });
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Sifre123" });
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new Session(doc.RootElement.GetProperty("token").GetString()!, doc.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session s, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        if (body != null) req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private static int _nextMovie = 999000;

    // Her test kendi filmleriyle: başka testlerin kullanıcıları aday olmaz.
    private async Task<int[]> MoviesAsync(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Interlocked.Increment(ref _nextMovie)).ToArray();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Movies.AddRange(ids.Select(id => new Movie { Id = id, Title = $"Film {id}" }));
        await db.SaveChangesAsync();
        return ids;
    }

    private static readonly int[] Taste = [10, 1, 10, 1, 10];

    private async Task RateAllAsync(Session s, int[] movies, int[] ratings)
    {
        for (var i = 0; i < movies.Length; i++)
        {
            var res = await SendAsync(HttpMethod.Post, "/api/v1/ratings", s, new { movieId = movies[i], rating = ratings[i] });
            res.EnsureSuccessStatusCode();
        }
    }

    private async Task<string?> TwinIdAsync(Session s)
    {
        var res = await SendAsync(HttpMethod.Get, "/api/v1/taste-twin", s);
        if (res.StatusCode == HttpStatusCode.NoContent) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("user").GetProperty("id").GetString();
    }

    private Task SetPrivacyAsync(Session s, string visibility, bool hidden) =>
        SendAsync(HttpMethod.Put, "/api/v1/account/privacy", s,
            new { profileVisibility = visibility, messagePermission = "Everyone", hiddenFromSearch = hidden });

    [Fact]
    public async Task EnUyumlu_KesfedilmeyeAcik_Kisi_HaftaBoyuncaAyni()
    {
        var movies = await MoviesAsync(6);
        int[] taste6 = [.. Taste, 1];
        var me = await RegisterAsync();
        var twin = await RegisterAsync();
        var hidden = await RegisterAsync();
        var privateTwin = await RegisterAsync();
        var followed = await RegisterAsync();
        var blocked = await RegisterAsync();
        var lukewarm = await RegisterAsync();
        await RateAllAsync(me, movies, taste6);
        // Uygun aday 5 ortak filmde; dışlanması gerekenler 6'da: kural
        // işlemeseydi eşit uyumda daha çok ortak film onları öne çıkarırdı.
        await RateAllAsync(twin, movies[..5], Taste);
        foreach (var u in new[] { hidden, privateTwin, followed, blocked })
        {
            await RateAllAsync(u, movies, taste6);
        }
        await RateAllAsync(lukewarm, movies[..5], [1, 10, 1, 10, 1]); // zıt zevk
        await SetPrivacyAsync(hidden, "Public", hidden: true);
        await SetPrivacyAsync(privateTwin, "Private", hidden: false);
        await SendAsync(HttpMethod.Post, $"/api/v1/follow/{followed.Id}", me);
        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{me.Id}", blocked);

        var res = await SendAsync(HttpMethod.Get, "/api/v1/taste-twin", me);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(twin.Id, doc.RootElement.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(100, doc.RootElement.GetProperty("compatibility").GetDouble());
        Assert.Equal(5, doc.RootElement.GetProperty("commonMovieCount").GetInt32());
        Assert.False(doc.RootElement.GetProperty("isFollowing").GetBoolean());

        Assert.Equal(twin.Id, await TwinIdAsync(me));

        // Öneriden sonra engellenirse gösterilmez.
        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{twin.Id}", me);
        Assert.Null(await TwinIdAsync(me));
    }

    [Fact]
    public async Task AzPuanVeyaUygunAdayYok_204()
    {
        var movies = await MoviesAsync(5);
        var newbie = await RegisterAsync();
        await RateAllAsync(newbie, movies[..3], Taste[..3]);
        Assert.Null(await TwinIdAsync(newbie));

        var me = await RegisterAsync();
        var opposite = await RegisterAsync();
        await RateAllAsync(me, movies, Taste);
        await RateAllAsync(opposite, movies, [1, 10, 1, 10, 1]);
        Assert.Null(await TwinIdAsync(me));
    }

    [Fact]
    public async Task YeniHafta_BaskaKisi_IsBirKezBildirir()
    {
        var movies = await MoviesAsync(5);
        var me = await RegisterAsync();
        var a = await RegisterAsync();
        var b = await RegisterAsync();
        foreach (var u in new[] { me, a, b }) await RateAllAsync(u, movies, Taste);

        var now = DateTime.UtcNow;
        var worker = _factory.Services.GetRequiredService<TasteTwinWorker>();
        await worker.RunOnceAsync(now, CancellationToken.None);
        await worker.RunOnceAsync(now, CancellationToken.None);

        string first;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mine = await db.TasteTwinSuggestions.Where(t => t.UserId == me.Id).ToListAsync();
            first = Assert.Single(mine).SuggestedUserId;
            Assert.True(mine[0].Notified);
            Assert.Contains(first, new[] { a.Id, b.Id });
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == me.Id && n.Message.StartsWith("Bu haftanın zevk ikizi")));

            // Aynı öneri ikinci kez bildirilmez.
            await scope.ServiceProvider.GetRequiredService<TasteTwinService>().NotifyAsync(mine[0]);
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == me.Id && n.Message.StartsWith("Bu haftanın zevk ikizi")));
        }

        // Gelecek hafta aynı kişi tekrar önerilmez.
        await worker.RunOnceAsync(now.AddDays(7), CancellationToken.None);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var week2 = TasteTwinService.WeekStartOf(now.AddDays(7));
            var second = await db.TasteTwinSuggestions.SingleAsync(t => t.UserId == me.Id && t.WeekStart == week2);
            Assert.NotEqual(first, second.SuggestedUserId);
            Assert.Contains(second.SuggestedUserId, new[] { a.Id, b.Id });
        }
    }

    [Theory]
    [InlineData("2026-10-19", "2026-10-19")] // pazartesi
    [InlineData("2026-10-25", "2026-10-19")] // pazar
    [InlineData("2026-10-21", "2026-10-19")]
    public void HaftaPazartesiBaslar(string day, string monday) =>
        Assert.Equal(DateOnly.Parse(monday), TasteTwinService.WeekStartOf(DateTime.Parse(day)));
}
