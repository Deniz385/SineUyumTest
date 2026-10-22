using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Tests;

// Film diary: dated entries (rewatches allowed, one per movie and day), the
// optional rating going through the ratings rules, edits by the owner only,
// the yearly summary and the profile-details privacy rule.
public class DiaryTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public DiaryTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id, string UserName);

    private async Task<Session> RegisterAsync()
    {
        var username = "di_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register", new { username, email = $"{username}@t.com", password = "Password123" });
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Password123" });
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new Session(doc.RootElement.GetProperty("token").GetString()!, doc.RootElement.GetProperty("user").GetProperty("id").GetString()!, username);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session s, object? body = null, string? language = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        if (language != null) req.Headers.AcceptLanguage.ParseAdd(language);
        if (body != null) req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static int _nextMovie = 970000;

    // A movie stored locally is logged without calling TMDb.
    private async Task<int> NewMovieAsync()
    {
        var id = Interlocked.Increment(ref _nextMovie);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Movies.Add(new Movie { Id = id, Title = $"Movie {id}", PosterPath = $"/p{id}.jpg" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<JsonElement> LogAsync(Session s, int movieId, string watchedOn, bool rewatch = false, string? note = null, int? rating = null)
    {
        var res = await SendAsync(HttpMethod.Post, "/api/v1/diary", s, new { movieId, watchedOn, rewatch, note, rating });
        Assert.True(res.StatusCode == HttpStatusCode.Created, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await JsonAsync(res);
    }

    private Task<HttpResponseMessage> ListAsync(Session viewer, Session owner, string query = "") =>
        SendAsync(HttpMethod.Get, $"/api/v1/diary/user/{owner.Id}{query}", viewer);

    private Task<HttpResponseMessage> SummaryAsync(Session viewer, Session owner, int year) =>
        SendAsync(HttpMethod.Get, $"/api/v1/diary/user/{owner.Id}/summary?year={year}", viewer);

    private Task SetVisibilityAsync(Session s, string visibility) =>
        SendAsync(HttpMethod.Put, "/api/v1/account/privacy", s,
            new { profileVisibility = visibility, messagePermission = "Everyone", hiddenFromSearch = false });

    private static string Today(int addDays = 0) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(addDays).ToString("yyyy-MM-dd");

    private async Task<UserRating?> StoredRatingAsync(Session s, int movieId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserRatings.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == s.Id && r.MovieId == movieId);
    }

    [Fact]
    public async Task Create_ListNewestFirst_Summary()
    {
        var me = await RegisterAsync();
        var viewer = await RegisterAsync();
        var m1 = await NewMovieAsync();
        var m2 = await NewMovieAsync();
        var m3 = await NewMovieAsync();

        var res = await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = m1, watchedOn = "2025-03-10" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal($"/api/v1/diary/user/{me.Id}", res.Headers.Location!.AbsolutePath);
        var first = await JsonAsync(res);
        Assert.Equal("2025-03-10", first.GetProperty("watchedOn").GetString());
        Assert.Equal(m1, first.GetProperty("movie").GetProperty("id").GetInt32());
        Assert.Equal($"Movie {m1}", first.GetProperty("movie").GetProperty("title").GetString());
        Assert.Equal($"/p{m1}.jpg", first.GetProperty("movie").GetProperty("posterPath").GetString());
        Assert.False(first.GetProperty("rewatch").GetBoolean());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("note").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("rating").ValueKind);

        var rewatch = await LogAsync(me, m1, "2025-11-01", rewatch: true, note: "  Even better the second time.  ");
        Assert.Equal("Even better the second time.", rewatch.GetProperty("note").GetString());
        var sameDay = await LogAsync(me, m2, "2025-03-10");
        await LogAsync(me, m3, "2024-07-04");

        // Newest date first; the same date by id, newest first.
        var list = await JsonAsync(await ListAsync(viewer, me));
        Assert.Equal(4, list.GetProperty("totalCount").GetInt32());
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["2025-11-01", "2025-03-10", "2025-03-10", "2024-07-04"], items.Select(i => i.GetProperty("watchedOn").GetString()));
        Assert.Equal(sameDay.GetProperty("id").GetInt32(), items[1].GetProperty("id").GetInt32());
        Assert.Equal(first.GetProperty("id").GetInt32(), items[2].GetProperty("id").GetInt32());
        Assert.True(items[0].GetProperty("rewatch").GetBoolean());

        // Paging.
        var page2 = await JsonAsync(await ListAsync(viewer, me, "?page=2&pageSize=3"));
        Assert.Equal(4, page2.GetProperty("totalCount").GetInt32());
        Assert.Equal("2024-07-04", Assert.Single(page2.GetProperty("items").EnumerateArray()).GetProperty("watchedOn").GetString());

        var summary = await JsonAsync(await SummaryAsync(viewer, me, 2025));
        Assert.Equal(2025, summary.GetProperty("year").GetInt32());
        Assert.Equal(3, summary.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("rewatchCount").GetInt32());
        var months = summary.GetProperty("months").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 12), months.Select(m => m.GetProperty("month").GetInt32()));
        Assert.Equal([0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 1, 0], months.Select(m => m.GetProperty("count").GetInt32()));
        Assert.Equal([2025, 2024], summary.GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()));

        // A year without entries: zeros, years still listed.
        var empty = await JsonAsync(await SummaryAsync(viewer, me, 2023));
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        Assert.All(empty.GetProperty("months").EnumerateArray(), m => Assert.Equal(0, m.GetProperty("count").GetInt32()));
        Assert.Equal(2, empty.GetProperty("availableYears").GetArrayLength());
    }

    // The "Add to diary" form asks for my entries of a movie to pre-select "Rewatch".
    [Fact]
    public async Task MineForMovie_OnlyMyEntriesOfThatMovie_NewestFirst()
    {
        var me = await RegisterAsync();
        var other = await RegisterAsync();
        var movie = await NewMovieAsync();
        var otherMovie = await NewMovieAsync();
        await LogAsync(me, movie, "2025-02-01");
        await LogAsync(me, movie, "2025-06-01", rewatch: true);
        await LogAsync(me, otherMovie, "2025-03-01");
        await LogAsync(other, movie, "2025-04-01");

        var mine = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/diary/movie/{movie}", me));
        Assert.Equal(["2025-06-01", "2025-02-01"], mine.EnumerateArray().Select(e => e.GetProperty("watchedOn").GetString()));
        Assert.All(mine.EnumerateArray(), e => Assert.Equal(movie, e.GetProperty("movie").GetProperty("id").GetInt32()));

        var none = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/diary/movie/{await NewMovieAsync()}", me));
        Assert.Empty(none.EnumerateArray());
    }

    [Fact]
    public async Task YearFilter_OnlyThatYear_InvalidYear400()
    {
        var me = await RegisterAsync();
        var m1 = await NewMovieAsync();
        var m2 = await NewMovieAsync();
        await LogAsync(me, m1, "2024-12-31");
        await LogAsync(me, m2, "2025-01-01");
        await LogAsync(me, m1, "2025-06-15", rewatch: true);

        var y2024 = await JsonAsync(await ListAsync(me, me, "?year=2024"));
        Assert.Equal(1, y2024.GetProperty("totalCount").GetInt32());
        Assert.Equal("2024-12-31", Assert.Single(y2024.GetProperty("items").EnumerateArray()).GetProperty("watchedOn").GetString());

        var y2025 = await JsonAsync(await ListAsync(me, me, "?year=2025"));
        Assert.Equal(["2025-06-15", "2025-01-01"], y2025.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("watchedOn").GetString()));

        Assert.Equal(0, (await JsonAsync(await ListAsync(me, me, "?year=2023"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await ListAsync(me, me, "?year=1800")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SummaryAsync(me, me, 99999)).StatusCode);
    }

    [Fact]
    public async Task Rating_IsUpsertedThroughTheRatingsRules()
    {
        var me = await RegisterAsync();
        var movie = await NewMovieAsync();

        var created = await LogAsync(me, movie, "2025-02-01", rating: 8);
        Assert.Equal(8, created.GetProperty("rating").GetInt32());
        var stored = await StoredRatingAsync(me, movie);
        Assert.Equal(8, stored!.Rating);
        // A new rating enters the feed like one given on the ratings endpoint.
        Assert.NotNull(stored.CreatedAt);
        Assert.Equal(stored.CreatedAt, stored.ActivityAt);

        // A rewatch with a new rating changes the same rating; it does not move up in the feed.
        await LogAsync(me, movie, "2025-09-01", rewatch: true, rating: 6);
        var changed = await StoredRatingAsync(me, movie);
        Assert.Equal(6, changed!.Rating);
        Assert.Equal(stored.ActivityAt, changed.ActivityAt);

        // An entry without a rating leaves it alone; every entry shows the current rating.
        await LogAsync(me, movie, "2025-10-01", rewatch: true);
        var items = (await JsonAsync(await ListAsync(me, me))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal(6, i.GetProperty("rating").GetInt32()));

        // Same result as the ratings endpoint reads.
        var reviews = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{movie}", me));
        Assert.Equal(6, reviews.GetProperty("myRating").GetInt32());

        // An invalid rating is rejected and nothing is written.
        var other = await NewMovieAsync();
        foreach (var bad in new[] { 0, 11 })
        {
            var res = await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = other, watchedOn = "2025-02-01", rating = bad });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        Assert.Null(await StoredRatingAsync(me, other));
        Assert.Equal(3, (await JsonAsync(await ListAsync(me, me))).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Duplicate_SameMovieSameDay_409_AndChangesNothing()
    {
        var me = await RegisterAsync();
        var movie = await NewMovieAsync();
        await LogAsync(me, movie, "2025-05-05", rating: 5);

        var dup = await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = movie, watchedOn = "2025-05-05", rewatch = true, rating = 9 });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("You have already logged this movie in your diary on this date.", (await JsonAsync(dup)).GetProperty("title").GetString());
        // The rejected request's rating was not written either.
        Assert.Equal(5, (await StoredRatingAsync(me, movie))!.Rating);

        // Another day is a rewatch, not a duplicate; another user's same day is fine too.
        var second = await LogAsync(me, movie, "2025-05-06", rewatch: true);
        var someoneElse = await RegisterAsync();
        await LogAsync(someoneElse, movie, "2025-05-05");

        // Moving an entry onto a day that already has the movie is a duplicate too.
        var moved = await SendAsync(HttpMethod.Put, $"/api/v1/diary/{second.GetProperty("id").GetInt32()}", me,
            new { watchedOn = "2025-05-05", rewatch = true });
        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.Equal(2, (await JsonAsync(await ListAsync(me, me))).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("en", "You have already logged this movie in your diary on this date.")]
    [InlineData("tr", "Bu filmi bu tarihte zaten günlüğüne eklemişsin.")]
    public async Task Duplicate_MessageInTheRequestLanguage(string language, string expected)
    {
        var me = await RegisterAsync();
        var movie = await NewMovieAsync();
        await LogAsync(me, movie, "2025-05-05");

        var dup = await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = movie, watchedOn = "2025-05-05" }, language);

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal(expected, (await JsonAsync(dup)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task WatchedOn_NotInTheFuture_NotBefore1900_Validated()
    {
        var me = await RegisterAsync();
        var movie = await NewMovieAsync();

        var future = await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = movie, watchedOn = Today(3) });
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal("The watch date cannot be in the future.", (await JsonAsync(future)).GetProperty("title").GetString());
        var futureTr = await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = movie, watchedOn = Today(3) }, "tr");
        Assert.Equal("İzleme tarihi gelecekte olamaz.", (await JsonAsync(futureTr)).GetProperty("title").GetString());

        var early = await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = movie, watchedOn = "1899-12-31" });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        // Malformed date, missing fields, a note that is too long.
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = movie, watchedOn = "2025-13-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { movieId = movie })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/v1/diary", me, new { watchedOn = "2025-01-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/v1/diary", me,
            new { movieId = movie, watchedOn = "2025-01-01", note = new string('x', 501) })).StatusCode);
        Assert.Equal(0, (await JsonAsync(await ListAsync(me, me))).GetProperty("totalCount").GetInt32());

        // Tomorrow (UTC) is allowed for time zones ahead of UTC; 1900-01-01 is the earliest day.
        await LogAsync(me, movie, Today(1));
        await LogAsync(me, movie, "1900-01-01");

        // The same rules apply on edit.
        var entry = await LogAsync(me, movie, "2020-01-01");
        var edit = await SendAsync(HttpMethod.Put, $"/api/v1/diary/{entry.GetProperty("id").GetInt32()}", me, new { watchedOn = Today(3) });
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
    }

    [Fact]
    public async Task EditAndDelete_OwnEntriesOnly()
    {
        var me = await RegisterAsync();
        var other = await RegisterAsync();
        var movie = await NewMovieAsync();
        var entry = await LogAsync(me, movie, "2025-04-01", note: "First time", rating: 7);
        var id = entry.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, $"/api/v1/diary/{id}", other,
            new { watchedOn = "2025-04-02", rewatch = true, note = "hijacked" })).StatusCode);
        var otherDelete = await SendAsync(HttpMethod.Delete, $"/api/v1/diary/{id}", other);
        Assert.Equal(HttpStatusCode.NotFound, otherDelete.StatusCode);
        Assert.Equal("Diary entry not found.", (await JsonAsync(otherDelete)).GetProperty("title").GetString());
        var unchanged = Assert.Single((await JsonAsync(await ListAsync(me, me))).GetProperty("items").EnumerateArray());
        Assert.Equal("2025-04-01", unchanged.GetProperty("watchedOn").GetString());
        Assert.Equal("First time", unchanged.GetProperty("note").GetString());

        var updated = await JsonAsync(await SendAsync(HttpMethod.Put, $"/api/v1/diary/{id}", me,
            new { watchedOn = "2025-04-02", rewatch = true, note = "  Second look  " }));
        Assert.Equal(id, updated.GetProperty("id").GetInt32());
        Assert.Equal("2025-04-02", updated.GetProperty("watchedOn").GetString());
        Assert.True(updated.GetProperty("rewatch").GetBoolean());
        Assert.Equal("Second look", updated.GetProperty("note").GetString());
        Assert.Equal(7, updated.GetProperty("rating").GetInt32());

        // An empty note clears it.
        var cleared = await JsonAsync(await SendAsync(HttpMethod.Put, $"/api/v1/diary/{id}", me,
            new { watchedOn = "2025-04-02", rewatch = false, note = "   " }));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("note").ValueKind);

        var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/diary/{id}", me);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal("Diary entry deleted.", (await JsonAsync(deleted)).GetProperty("message").GetString());
        Assert.Equal(0, (await JsonAsync(await ListAsync(me, me))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"/api/v1/diary/{id}", me)).StatusCode);
        // Deleting the entry keeps the rating (the opinion is separate from the log).
        Assert.Equal(7, (await StoredRatingAsync(me, movie))!.Rating);
    }

    [Fact]
    public async Task PrivateProfile_HidesDiaryFromOthers_OwnerAlwaysSees()
    {
        var owner = await RegisterAsync();
        var follower = await RegisterAsync();
        var stranger = await RegisterAsync();
        var movie = await NewMovieAsync();
        await LogAsync(owner, movie, "2025-08-08");

        Assert.Equal(HttpStatusCode.OK, (await ListAsync(stranger, owner)).StatusCode);

        await SetVisibilityAsync(owner, "Private");
        var hidden = await ListAsync(stranger, owner);
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        Assert.Equal("This user's profile details are private.", (await JsonAsync(hidden)).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await SummaryAsync(stranger, owner, 2025)).StatusCode);
        Assert.Equal(1, (await JsonAsync(await ListAsync(owner, owner))).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await JsonAsync(await SummaryAsync(owner, owner, 2025))).GetProperty("totalCount").GetInt32());

        // "Following": visible to people the owner follows only.
        await SetVisibilityAsync(owner, "Following");
        await SendAsync(HttpMethod.Post, $"/api/v1/follow/{follower.Id}", owner);
        Assert.Equal(HttpStatusCode.OK, (await ListAsync(follower, owner)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ListAsync(stranger, owner)).StatusCode);

        // An unknown user is 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, "/api/v1/diary/user/missing-user", stranger)).StatusCode);
    }

    [Fact]
    public async Task Block_HidesDiaryInBothDirections_OwnerAlwaysSees()
    {
        var owner = await RegisterAsync();
        var viewer = await RegisterAsync();
        var movie = await NewMovieAsync();
        await LogAsync(owner, movie, "2025-08-08");
        await LogAsync(viewer, movie, "2025-08-09");

        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{viewer.Id}", owner);
        Assert.Equal(HttpStatusCode.NotFound, (await ListAsync(viewer, owner)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SummaryAsync(viewer, owner, 2025)).StatusCode);
        // The blocker does not see the blocked user's diary either.
        Assert.Equal(HttpStatusCode.NotFound, (await ListAsync(owner, viewer)).StatusCode);
        Assert.Equal(1, (await JsonAsync(await ListAsync(owner, owner))).GetProperty("totalCount").GetInt32());

        await SendAsync(HttpMethod.Delete, $"/api/v1/blocks/{viewer.Id}", owner);
        Assert.Equal(HttpStatusCode.OK, (await ListAsync(viewer, owner)).StatusCode);
    }
}
