using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// Türler önceden hiç yazılmıyordu. Yeni filmde TMDb türleri kaydedilir,
// eskiler arka planda tamamlanır; uyum açıklaması bunları kullanır.
public class GenreTests : IClassFixture<GenreTests.Factory>
{
    // Film detayı: id 960404 TMDb'de yok; diğerleri id'ye göre türlerle döner.
    public sealed class FakeTmdb : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath; // /3/movie/{id}
            var id = int.Parse(path.Split('/').Last());
            if (id == 960404) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var genres = id % 2 == 0
                ? "[{\"id\":18,\"name\":\"Dram\"},{\"id\":878,\"name\":\"Bilim-Kurgu\"}]"
                : "[{\"id\":27,\"name\":\"Korku\"}]";
            var body = $"{{\"id\":{id},\"title\":\"Film {id}\",\"poster_path\":\"/p{id}.jpg\",\"vote_average\":7.84,\"genres\":{genres}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    public sealed class Factory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> { ["TMDb:ApiKey"] = "k" }));
            builder.ConfigureServices(s => s.AddHttpClient("TMDb").ConfigurePrimaryHttpMessageHandler(() => new FakeTmdb()));
        }
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;

    public GenreTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id);

    private async Task<Session> RegisterAsync()
    {
        var username = "gn_" + Guid.NewGuid().ToString("N")[..10];
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

    private async Task RateAsync(Session s, int movieId, int rating)
    {
        var res = await SendAsync(HttpMethod.Post, "/api/v1/ratings", s, new { movieId, rating });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<List<string>> GenresOfAsync(int movieId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.MovieGenres.Where(mg => mg.MovieId == movieId).Select(mg => mg.Genre.Name).OrderBy(n => n).ToListAsync();
    }

    [Fact]
    public async Task YeniFilmde_TmdbTurleriKaydedilir()
    {
        var me = await RegisterAsync();
        await RateAsync(me, 960002, 8);
        Assert.Equal(["Bilim-Kurgu", "Dram"], await GenresOfAsync(960002));
        using (var scope = _factory.Services.CreateScope())
        {
            // TMDb ortalaması da saklanır (kimlik kartındaki "en aykırı puan" için).
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(7.8, (await db.Movies.FindAsync(960002))!.TmdbVoteAverage);
        }
        // Aynı türü paylaşan ikinci film: tür tekrar eklenmez (birincil anahtar çakışmaz).
        await RateAsync(me, 960004, 7);
        Assert.Equal(["Bilim-Kurgu", "Dram"], await GenresOfAsync(960004));
    }

    [Fact]
    public async Task EskiFilmler_ArkaPlandaTamamlanir_TmdbdeOlmayanTekrarSorulmaz()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Movies.AddRange(new Movie { Id = 960011, Title = "Eski" }, new Movie { Id = 960404, Title = "TMDb'de yok" });
            await db.SaveChangesAsync();
        }

        var worker = _factory.Services.GetRequiredService<GenreBackfillWorker>();
        await worker.RunBatchAsync(CancellationToken.None);

        Assert.Equal(["Korku"], await GenresOfAsync(960011));
        using var check = _factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await context.Movies.CountAsync(m => (m.Id == 960011 || m.Id == 960404) && m.GenresSyncedAt == null));
    }

    [Fact]
    public async Task UyumYaniti_AciklamaVeTartismaFilmiIcerir()
    {
        var me = await RegisterAsync();
        var them = await RegisterAsync();
        // Çift id: Dram + Bilim-Kurgu, tek id: Korku.
        foreach (var (id, mine, theirs) in new[] { (960102, 9, 9), (960104, 8, 9), (960101, 10, 2), (960103, 9, 3) })
        {
            await RateAsync(me, id, mine);
            await RateAsync(them, id, theirs);
        }

        var res = await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{them.Id}", me);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var insights = doc.RootElement.GetProperty("insights");

        Assert.Equal("İkiniz de Bilim-Kurgu ve Dram filmlerini seviyorsunuz; Korku filmlerinde ayrışıyorsunuz.",
            insights.GetProperty("summary").GetString());
        Assert.Equal(960101, insights.GetProperty("debateMovie").GetProperty("movieId").GetInt32());
        Assert.Equal(10, insights.GetProperty("debateMovie").GetProperty("currentUserRating").GetInt32());
        Assert.Equal(960102, insights.GetProperty("sharedFavorite").GetProperty("movieId").GetInt32());
    }

    [Fact]
    public async Task KimlikKarti_PuanlardanUretilir_GizlilikeUyar()
    {
        var me = await RegisterAsync();
        var viewer = await RegisterAsync();
        // Çift id: Dram + Bilim-Kurgu; TMDb ortalaması 7.8.
        foreach (var (id, rating) in new[] { (960202, 9), (960204, 10), (960206, 8), (960208, 9), (960210, 2) })
        {
            await RateAsync(me, id, rating);
        }

        var res = await SendAsync(HttpMethod.Get, $"/api/v1/profile/{me.Id}/identity", viewer);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(5, root.GetProperty("totalRatings").GetInt32());
        Assert.Equal(7.6, root.GetProperty("averageRating").GetDouble());
        Assert.Equal(["Bilim-Kurgu", "Dram"], root.GetProperty("topGenres").EnumerateArray().Select(g => g.GetProperty("name").GetString()));
        Assert.Equal(960210, root.GetProperty("contrarianTake").GetProperty("movieId").GetInt32());
        Assert.Equal(960204, root.GetProperty("favorite").GetProperty("movieId").GetInt32());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("title").GetString()));

        await SendAsync(HttpMethod.Put, "/api/v1/account/privacy", me,
            new { profileVisibility = "Private", messagePermission = "Everyone", hiddenFromSearch = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, $"/api/v1/profile/{me.Id}/identity", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"/api/v1/profile/{me.Id}/identity", me)).StatusCode);
    }
}
