using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Tests;

// First-run welcome state and people suggestions.
public class OnboardingAndPeopleTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public OnboardingAndPeopleTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id, JsonElement LoginUser);

    private async Task<Session> RegisterAsync()
    {
        var username = "ob_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register", new { username, email = $"{username}@t.com", password = "Password123" });
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Password123" });
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var user = doc.RootElement.GetProperty("user").Clone();
        return new Session(doc.RootElement.GetProperty("token").GetString()!, user.GetProperty("id").GetString()!, user);
    }

    private async Task<JsonElement> JsonAsync(HttpMethod method, string url, Session s)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        var res = await _client.SendAsync(req);
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

    private static int _nextMovie = 995000;

    [Fact]
    public async Task NewAccount_SeesWelcomeUntilCompleted_Idempotent()
    {
        var me = await RegisterAsync();
        Assert.False(me.LoginUser.GetProperty("onboardingCompleted").GetBoolean());
        Assert.False((await JsonAsync(HttpMethod.Get, "/api/v1/account/me", me)).GetProperty("onboardingCompleted").GetBoolean());

        Assert.True((await JsonAsync(HttpMethod.Post, "/api/v1/account/onboarding/complete", me)).GetProperty("onboardingCompleted").GetBoolean());
        Assert.True((await JsonAsync(HttpMethod.Post, "/api/v1/account/onboarding/complete", me)).GetProperty("onboardingCompleted").GetBoolean());
        Assert.True((await JsonAsync(HttpMethod.Get, "/api/v1/account/me", me)).GetProperty("onboardingCompleted").GetBoolean());
    }

    // Only people open to discovery, not followed, not blocked; compatible ones first.
    [Fact]
    public async Task Suggestions_ByTaste_OnlyOpenUnfollowedCompatiblePeople()
    {
        var me = await RegisterAsync();
        var twin = await RegisterAsync();
        var close = await RegisterAsync();
        var opposite = await RegisterAsync();
        var privateTwin = await RegisterAsync();
        var hiddenTwin = await RegisterAsync();
        var followedTwin = await RegisterAsync();
        var blockedTwin = await RegisterAsync();
        int[] mine = [9, 8, 2, 7];

        await SeedAsync(db =>
        {
            for (var i = 0; i < mine.Length; i++)
            {
                var id = Interlocked.Increment(ref _nextMovie);
                db.Movies.Add(new Movie { Id = id, Title = $"M{id}" });
                db.UserRatings.Add(new UserRating { UserId = me.Id, MovieId = id, Rating = mine[i] });
                foreach (var twinLike in new[] { twin, privateTwin, hiddenTwin, followedTwin, blockedTwin })
                {
                    db.UserRatings.Add(new UserRating { UserId = twinLike.Id, MovieId = id, Rating = mine[i] });
                }
                db.UserRatings.Add(new UserRating { UserId = close.Id, MovieId = id, Rating = Math.Clamp(mine[i] + 1, 1, 10) });
                db.UserRatings.Add(new UserRating { UserId = opposite.Id, MovieId = id, Rating = 11 - mine[i] });
            }
            db.Users.Single(u => u.Id == privateTwin.Id).ProfileVisibility = ProfileVisibility.Private;
            db.Users.Single(u => u.Id == hiddenTwin.Id).HiddenFromSearch = true;
            db.UserFollows.Add(new UserFollow { FollowerId = me.Id, FollowingId = followedTwin.Id });
            db.UserBlocks.Add(new UserBlock { BlockerId = blockedTwin.Id, BlockedId = me.Id });
        });

        var result = await JsonAsync(HttpMethod.Get, "/api/v1/people/suggestions", me);
        Assert.Equal("taste", result.GetProperty("basis").GetString());
        var ids = result.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("user").GetProperty("id").GetString()).ToList();
        Assert.Equal([twin.Id, close.Id], ids);
        var first = result.GetProperty("items")[0];
        Assert.Equal(4, first.GetProperty("commonMovieCount").GetInt32());
        Assert.True(first.GetProperty("compatibility").GetDouble() > result.GetProperty("items")[1].GetProperty("compatibility").GetDouble());
    }

    // Without enough ratings: the most followed people who have rated something.
    [Fact]
    public async Task Suggestions_FallBackToPopularPeople()
    {
        var newcomer = await RegisterAsync();
        var star = await RegisterAsync();
        var known = await RegisterAsync();
        var silent = await RegisterAsync();
        var fans = new List<Session>();
        for (var i = 0; i < 3; i++) fans.Add(await RegisterAsync());

        await SeedAsync(db =>
        {
            var id = Interlocked.Increment(ref _nextMovie);
            db.Movies.Add(new Movie { Id = id, Title = $"M{id}" });
            db.UserRatings.Add(new UserRating { UserId = star.Id, MovieId = id, Rating = 8 });
            db.UserRatings.Add(new UserRating { UserId = known.Id, MovieId = id, Rating = 6 });
            foreach (var fan in fans)
            {
                db.UserFollows.Add(new UserFollow { FollowerId = fan.Id, FollowingId = star.Id });
                db.UserFollows.Add(new UserFollow { FollowerId = fan.Id, FollowingId = silent.Id });
            }
            db.UserFollows.Add(new UserFollow { FollowerId = fans[0].Id, FollowingId = known.Id });
        });

        var result = await JsonAsync(HttpMethod.Get, "/api/v1/people/suggestions?limit=30", newcomer);
        Assert.Equal("popular", result.GetProperty("basis").GetString());
        var ids = result.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("user").GetProperty("id").GetString()).ToList();
        // silent has followers but no ratings yet: not suggested.
        Assert.DoesNotContain(silent.Id, ids);
        Assert.DoesNotContain(newcomer.Id, ids);
        Assert.True(ids.IndexOf(star.Id) < ids.IndexOf(known.Id));
        var starItem = result.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("user").GetProperty("id").GetString() == star.Id);
        Assert.Equal(3, starItem.GetProperty("followerCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, starItem.GetProperty("compatibility").ValueKind);
    }
}
