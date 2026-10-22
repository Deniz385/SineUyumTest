using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Tests;

// The yearly recap: the year's movies are the diary entries of that year plus
// the movies first rated that year; their current ratings form the profile.
public class YearInReviewTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public YearInReviewTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id);

    private async Task<Session> RegisterAsync()
    {
        var username = "yr_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register", new { username, email = $"{username}@t.com", password = "Password123" });
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Password123" });
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new Session(doc.RootElement.GetProperty("token").GetString()!, doc.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    private async Task<HttpResponseMessage> GetAsync(Session viewer, string userId, string query = "", string? language = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/year-in-review/user/{userId}{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", viewer.Token);
        if (language != null) req.Headers.AcceptLanguage.ParseAdd(language);
        return await _client.SendAsync(req);
    }

    private async Task<JsonElement> RecapAsync(Session viewer, string userId, string query = "", string? language = null)
    {
        var res = await GetAsync(viewer, userId, query, language);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private async Task SeedAsync(Action<ApplicationDbContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static int _nextMovie = 980000;
    private static int NextMovieId() => Interlocked.Increment(ref _nextMovie);

    private async Task<int> MovieAsync(double? tmdb = null, int? genreId = null, string? genreName = null)
    {
        var id = NextMovieId();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var movie = new Movie { Id = id, Title = $"Movie {id}", PosterPath = $"/p{id}.jpg", TmdbVoteAverage = tmdb };
        if (genreId != null)
        {
            var genre = await db.Genres.FindAsync(genreId.Value) ?? db.Genres.Add(new Genre { Id = genreId.Value, Name = genreName! }).Entity;
            movie.MovieGenres.Add(new MovieGenre { Movie = movie, Genre = genre });
        }
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        return id;
    }

    private static UserRating Rating(string userId, int movieId, int rating, string? createdOn = null) => new()
    {
        UserId = userId,
        MovieId = movieId,
        Rating = rating,
        CreatedAt = createdOn == null ? null : DateTime.SpecifyKind(DateTime.Parse(createdOn), DateTimeKind.Utc)
    };

    private static DiaryEntry Logged(string userId, int movieId, string watchedOn, bool rewatch = false) => new()
    {
        UserId = userId, MovieId = movieId, WatchedOn = DateOnly.Parse(watchedOn), Rewatch = rewatch
    };

    private static DateTime Utc(string date) => DateTime.SpecifyKind(DateTime.Parse(date), DateTimeKind.Utc);

    [Fact]
    public async Task Year_BuiltFromDiaryAndThatYearsRatings()
    {
        var me = await RegisterAsync();
        var fan = await RegisterAsync();
        var drama1 = await MovieAsync(8.0, 18, "Drama");
        var drama2 = await MovieAsync(7.0, 18, "Drama");
        var horror1 = await MovieAsync(8.5, 27, "Horror");
        var horror2 = await MovieAsync(6.0, 27, "Horror");
        var lastYear1 = await MovieAsync();
        var lastYear2 = await MovieAsync();

        await SeedAsync(db =>
        {
            // 2025: drama1 logged + rated; drama2 logged three times (an old,
            // undated rating); horror1 only rated; horror2 logged (rated in 2024).
            db.UserRatings.AddRange(
                Rating(me.Id, drama1, 9, "2025-01-05"),
                Rating(me.Id, drama2, 10),
                Rating(me.Id, horror1, 3, "2025-03-20"),
                Rating(me.Id, horror2, 6, "2024-12-30"),
                Rating(me.Id, lastYear1, 8, "2024-06-01"),
                Rating(me.Id, lastYear2, 4),
                Rating(fan.Id, drama1, 2, "2025-05-05"));
            db.DiaryEntries.AddRange(
                Logged(me.Id, drama1, "2025-01-05"),            // Sunday
                Logged(me.Id, drama2, "2025-03-09"),            // Sunday
                Logged(me.Id, drama2, "2025-03-16", true),      // Sunday
                Logged(me.Id, horror2, "2025-07-12"),           // Saturday
                Logged(me.Id, drama2, "2025-11-02", true),      // Sunday
                Logged(me.Id, lastYear2, "2024-05-05"));
            var review = new Review { UserId = me.Id, MovieId = drama1, Text = "Great", CreatedAt = Utc("2025-02-01") };
            db.Reviews.Add(review);
            db.ReviewLikes.Add(new ReviewLike { Review = review, UserId = fan.Id, CreatedAt = Utc("2025-02-02") });
            db.ReviewComments.AddRange(
                new ReviewComment { Review = review, UserId = fan.Id, Text = "Agree", CreatedAt = Utc("2025-02-03") },
                new ReviewComment { Review = review, UserId = me.Id, Text = "Thanks", CreatedAt = Utc("2025-02-04") },
                new ReviewComment { Review = review, UserId = fan.Id, Text = "Later", CreatedAt = Utc("2026-01-02") });
        });

        var recap = await RecapAsync(me, me.Id, "?year=2025", "en");

        Assert.True(recap.GetProperty("ready").GetBoolean());
        Assert.True(recap.GetProperty("isMine").GetBoolean());
        Assert.Equal([2025, 2024], recap.GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()));

        var totals = recap.GetProperty("totals");
        Assert.Equal(4, totals.GetProperty("movies").GetInt32());
        Assert.Equal(5, totals.GetProperty("diaryEntries").GetInt32());
        Assert.Equal(2, totals.GetProperty("rewatches").GetInt32());
        Assert.Equal(2, totals.GetProperty("ratings").GetInt32());
        Assert.Equal(1, totals.GetProperty("reviews").GetInt32());
        Assert.Equal(1, totals.GetProperty("likesReceived").GetInt32());
        Assert.Equal(1, totals.GetProperty("commentsReceived").GetInt32());

        // horror1 has no diary entry: counted in March by its rating date.
        Assert.Equal([1, 0, 3, 0, 0, 0, 1, 0, 0, 0, 1, 0],
            recap.GetProperty("months").EnumerateArray().Select(m => m.GetProperty("count").GetInt32()));
        Assert.Equal(3, recap.GetProperty("busiestMonth").GetProperty("month").GetInt32());
        // ISO weekday: Sunday is 7.
        Assert.Equal(7, recap.GetProperty("favoriteWeekday").GetProperty("weekday").GetInt32());
        Assert.Equal(4, recap.GetProperty("favoriteWeekday").GetProperty("count").GetInt32());

        Assert.Equal(7.0, recap.GetProperty("averageRating").GetDouble());
        Assert.Equal(6.0, recap.GetProperty("previousYearAverage").GetDouble());
        Assert.Equal([drama2, drama1, horror2, horror1],
            recap.GetProperty("topMovies").EnumerateArray().Select(m => m.GetProperty("movieId").GetInt32()));

        Assert.Equal(drama1, recap.GetProperty("firstMovie").GetProperty("movie").GetProperty("id").GetInt32());
        Assert.Equal("2025-01-05", recap.GetProperty("firstMovie").GetProperty("watchedOn").GetString());
        Assert.Equal(drama2, recap.GetProperty("lastMovie").GetProperty("movie").GetProperty("id").GetInt32());
        Assert.Equal(drama2, recap.GetProperty("mostRewatched").GetProperty("movie").GetProperty("id").GetInt32());
        Assert.Equal(3, recap.GetProperty("mostRewatched").GetProperty("count").GetInt32());

        var identity = recap.GetProperty("identity");
        Assert.Equal(["Drama", "Horror"], identity.GetProperty("topGenres").EnumerateArray().Select(g => g.GetProperty("name").GetString()));
        Assert.Equal(horror1, identity.GetProperty("contrarianTake").GetProperty("movieId").GetInt32());

        // Genre names and the yearly title follow the request language.
        var turkish = await RecapAsync(me, me.Id, "?year=2025", "tr");
        Assert.Equal(["Dram", "Korku"], turkish.GetProperty("identity").GetProperty("topGenres").EnumerateArray().Select(g => g.GetProperty("name").GetString()));
        Assert.NotEqual(identity.GetProperty("title").GetString(), turkish.GetProperty("identity").GetProperty("title").GetString());
    }

    [Fact]
    public async Task FewMovies_NotReady_DefaultsToCurrentYear()
    {
        var me = await RegisterAsync();
        var a = await MovieAsync();
        var b = await MovieAsync();
        var now = DateTime.UtcNow;
        await SeedAsync(db =>
        {
            db.UserRatings.Add(new UserRating { UserId = me.Id, MovieId = a, Rating = 7, CreatedAt = now });
            db.DiaryEntries.Add(Logged(me.Id, b, DateOnly.FromDateTime(now).ToString("yyyy-MM-dd")));
        });

        var recap = await RecapAsync(me, me.Id);

        Assert.Equal(now.Year, recap.GetProperty("year").GetInt32());
        Assert.False(recap.GetProperty("ready").GetBoolean());
        Assert.Equal(3, recap.GetProperty("minMovies").GetInt32());
        Assert.Equal(2, recap.GetProperty("totals").GetProperty("movies").GetInt32());
        Assert.Equal(JsonValueKind.Null, recap.GetProperty("favoriteWeekday").ValueKind);
        Assert.Equal(JsonValueKind.Null, recap.GetProperty("mostRewatched").ValueKind);
    }

    // The most compatible person the owner FOLLOWS, with at least 5 movies in common.
    [Fact]
    public async Task TasteMate_MostCompatibleFollowedPersonWithEnoughCommonMovies()
    {
        var me = await RegisterAsync();
        var close = await RegisterAsync();
        var further = await RegisterAsync();
        var fewCommon = await RegisterAsync();
        var notFollowed = await RegisterAsync();
        var movies = new List<int>();
        for (var i = 0; i < 6; i++) movies.Add(await MovieAsync());
        int[] mine = [9, 8, 3, 7, 10, 2];

        await SeedAsync(db =>
        {
            for (var i = 0; i < movies.Count; i++)
            {
                db.UserRatings.Add(Rating(me.Id, movies[i], mine[i]));
                db.UserRatings.Add(Rating(close.Id, movies[i], Math.Clamp(mine[i] - 1, 1, 10)));
                db.UserRatings.Add(Rating(further.Id, movies[i], 11 - mine[i]));
                db.UserRatings.Add(Rating(notFollowed.Id, movies[i], mine[i]));
                if (i < 4) db.UserRatings.Add(Rating(fewCommon.Id, movies[i], mine[i]));
            }
            foreach (var followed in new[] { close, further, fewCommon })
            {
                db.UserFollows.Add(new UserFollow { FollowerId = me.Id, FollowingId = followed.Id });
            }
            // A perfect match followed by someone else does not count.
            db.UserFollows.Add(new UserFollow { FollowerId = further.Id, FollowingId = notFollowed.Id });
        });

        var mate = (await RecapAsync(me, me.Id)).GetProperty("tasteMate");
        Assert.Equal(close.Id, mate.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(6, mate.GetProperty("commonCount").GetInt32());
    }

    [Fact]
    public async Task PrivateProfile_HiddenFromOthers_OwnerAlwaysSees_Blocked404()
    {
        var owner = await RegisterAsync();
        var other = await RegisterAsync();
        var blocked = await RegisterAsync();

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(other, owner.Id)).StatusCode);

        await SeedAsync(db =>
        {
            db.Users.Single(u => u.Id == owner.Id).ProfileVisibility = ProfileVisibility.Private;
            db.UserBlocks.Add(new UserBlock { BlockerId = owner.Id, BlockedId = blocked.Id });
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(other, owner.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(blocked, owner.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(owner, owner.Id)).StatusCode);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(3000)]
    public async Task InvalidYear_400(int year)
    {
        var me = await RegisterAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await GetAsync(me, me.Id, $"?year={year}")).StatusCode);
    }
}
