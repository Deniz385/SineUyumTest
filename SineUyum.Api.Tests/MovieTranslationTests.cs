using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// Movie titles are stored per language: lists, the feed, the diary and
// notifications show the title in the reader's language, falling back to the
// stored title until that language's title is known.
public class MovieTranslationTests : IClassFixture<MovieTranslationTests.Factory>
{
    // Turkish requests get "Film {id}", everything else "Movie {id}"; 970404 is not on TMDb.
    public sealed class FakeTmdb : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            Requests.Enqueue(uri.PathAndQuery);
            var id = int.Parse(uri.AbsolutePath.Split('/').Last());
            if (id == 970404) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var title = uri.Query.Contains("language=tr-TR") ? $"Film {id}" : $"Movie {id}";
            var body = $"{{\"id\":{id},\"title\":\"{title}\",\"poster_path\":\"/p{id}.jpg\",\"genres\":[],\"credits\":{{\"cast\":[]}}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    public sealed class Factory : FakePushFactory
    {
        public FakeTmdb Tmdb { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> { ["TMDb:ApiKey"] = "k" }));
            builder.ConfigureServices(s => s.AddHttpClient("TMDb").ConfigurePrimaryHttpMessageHandler(() => Tmdb));
        }
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;

    public MovieTranslationTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> GetJsonAsync(string url, Session s, string language)
    {
        var res = await _client.SendAsync(Authed(HttpMethod.Get, url, s.Token, language: language));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task SendOkAsync(HttpMethod method, string url, Session s, object? body = null, string? language = null)
    {
        var res = await _client.SendAsync(Authed(method, url, s.Token, body, language));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<string?> ProfileTitleAsync(Session viewer, string userId, int movieId, string language)
    {
        var profile = await GetJsonAsync($"/api/v1/profile/{userId}", viewer, language);
        return profile.GetProperty("ratings").EnumerateArray()
            .Single(r => r.GetProperty("movieId").GetInt32() == movieId)
            .GetProperty("title").GetString();
    }

    private async Task<Dictionary<string, string>> TitlesOfAsync(int movieId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.MovieTranslations.Where(t => t.MovieId == movieId).ToDictionaryAsync(t => t.Language, t => t.Title);
    }

    private async Task SeedMovieAsync(int movieId, string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Movies.Add(new Movie { Id = movieId, Title = title });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task NewMovie_StoredInRequestLanguage_OtherLanguagesFallBackUntilFilledIn()
    {
        var me = await RegisterAsync(_client, "mt_", "tr");
        await SendOkAsync(HttpMethod.Post, "/api/v1/ratings", me, new { movieId = 970001, rating = 8 }, "tr");

        Assert.Equal(new Dictionary<string, string> { ["tr"] = "Film 970001" }, await TitlesOfAsync(970001));
        Assert.Equal("Film 970001", await ProfileTitleAsync(me, me.UserId, 970001, "tr"));
        // No English title yet: the stored (Turkish) title.
        Assert.Equal("Film 970001", await ProfileTitleAsync(me, me.UserId, 970001, "en"));

        await _factory.Services.GetRequiredService<MovieTranslationWorker>().RunBatchAsync(CancellationToken.None);

        Assert.Equal("Movie 970001", await ProfileTitleAsync(me, me.UserId, 970001, "en"));
        Assert.Equal("Film 970001", await ProfileTitleAsync(me, me.UserId, 970001, "tr"));
    }

    [Fact]
    public async Task ExistingMovie_RatedOrOpenedInAnotherLanguage_GetsThatTitle()
    {
        await SeedMovieAsync(970010, "Eski ad");
        await SeedMovieAsync(970011, "Eski ad 2");
        var me = await RegisterAsync(_client, "mt_", "en");

        // Rating (and watchlists, the diary) ensure the title in the request language.
        await SendOkAsync(HttpMethod.Post, "/api/v1/ratings", me, new { movieId = 970010, rating = 7 }, "en");
        Assert.Equal("Movie 970010", (await TitlesOfAsync(970010))["en"]);

        // The details page remembers it for catalog movies...
        await SendOkAsync(HttpMethod.Get, "/api/v1/movies/970011", me, language: "tr");
        Assert.Equal("Film 970011", (await TitlesOfAsync(970011))["tr"]);

        // ...but does not add movies to the catalog.
        await SendOkAsync(HttpMethod.Get, "/api/v1/movies/970012", me, language: "tr");
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Movies.AnyAsync(m => m.Id == 970012));
    }

    // One query plan serves every language: switching back and forth must not
    // leak a title into the other language (watchlist, diary, feed).
    [Fact]
    public async Task Lists_DiaryAndFeed_FollowTheRequestLanguage()
    {
        var me = await RegisterAsync(_client, "mt_", "tr");
        await SendOkAsync(HttpMethod.Post, "/api/v1/ratings", me, new { movieId = 970020, rating = 9 }, "tr");
        await SendOkAsync(HttpMethod.Post, "/api/v1/ratings", me, new { movieId = 970020, rating = 9 }, "en");

        var list = await _client.SendAsync(Authed(HttpMethod.Post, "/api/v1/watchlist", me.Token, new { name = "L" }));
        var listId = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
        await SendOkAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/movies", me, new { movieId = 970020 });
        await SendOkAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = 970020, watchedOn = "2026-01-05" });

        foreach (var (language, expected) in new[] { ("tr", "Film 970020"), ("en", "Movie 970020"), ("tr", "Film 970020") })
        {
            var watchlist = await GetJsonAsync($"/api/v1/watchlist/{listId}", me, language);
            Assert.Equal(expected, watchlist.GetProperty("items")[0].GetProperty("title").GetString());

            var diary = await GetJsonAsync($"/api/v1/diary/user/{me.UserId}", me, language);
            Assert.Equal(expected, diary.GetProperty("items")[0].GetProperty("movie").GetProperty("title").GetString());

            var feed = await GetJsonAsync("/api/v1/feed", me, language);
            Assert.Equal(expected, feed.GetProperty("items").EnumerateArray()
                .First(i => i.GetProperty("movie").GetProperty("id").GetInt32() == 970020)
                .GetProperty("movie").GetProperty("title").GetString());
        }
    }

    // The movie in a notification follows the reader: the push in the
    // recipient's language, the list in the request language.
    [Fact]
    public async Task Notification_MovieTitleFollowsTheReadersLanguage()
    {
        var author = await RegisterAsync(_client, "mt_", "tr");
        var fan = await RegisterAsync(_client, "mt_", "en");
        var device = "fcm-" + Guid.NewGuid().ToString("N");
        await RegisterDeviceAsync(_client, author, device);

        await SendOkAsync(HttpMethod.Post, "/api/v1/ratings", author, new { movieId = 970030, rating = 9 }, "tr");
        await SendOkAsync(HttpMethod.Post, "/api/v1/ratings", fan, new { movieId = 970030, rating = 8 }, "en");
        await SendOkAsync(HttpMethod.Put, "/api/v1/reviews/movie/970030", author, new { text = "Great", containsSpoilers = false });
        var reviews = await GetJsonAsync("/api/v1/reviews/movie/970030", fan, "en");
        var reviewId = reviews.GetProperty("reviews")[0].GetProperty("id").GetInt32();
        await SendOkAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/like", fan);

        await Eventually(() => _factory.Push.Sent.Any(s => s.DeviceToken == device), "push to the author");
        Assert.Contains("'Film 970030'", _factory.Push.Sent.Single(s => s.DeviceToken == device).Message.Body);

        async Task<string> ListedAsync(string language) =>
            (await GetJsonAsync("/api/v1/notification", author, language))[0].GetProperty("message").GetString()!;
        Assert.EndsWith("liked your review of 'Movie 970030'.", await ListedAsync("en"));
        Assert.EndsWith("'Film 970030' incelemeni beğendi.", await ListedAsync("tr"));
    }

    [Fact]
    public async Task Worker_FillsEveryLanguage_MoviesMissingOnTmdbKeepTheirTitleAndAreNotAskedAgain()
    {
        await SeedMovieAsync(970040, "Bir film");
        await SeedMovieAsync(970404, "TMDb'de yok");
        var worker = _factory.Services.GetRequiredService<MovieTranslationWorker>();

        // Other tests' movies may be in the same database: run until nothing is left.
        while (await worker.RunBatchAsync(CancellationToken.None) > 0) { }

        Assert.Equal(new Dictionary<string, string> { ["en"] = "Movie 970040", ["tr"] = "Film 970040" }, await TitlesOfAsync(970040));
        Assert.Equal(new Dictionary<string, string> { ["en"] = "TMDb'de yok", ["tr"] = "TMDb'de yok" }, await TitlesOfAsync(970404));

        var before = _factory.Tmdb.Requests.Count;
        Assert.Equal(0, await worker.RunBatchAsync(CancellationToken.None));
        Assert.Equal(before, _factory.Tmdb.Requests.Count);
    }
}
