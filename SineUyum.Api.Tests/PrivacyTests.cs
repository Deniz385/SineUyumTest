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

// G12: profile visibility, message permission, hiding from search and blocking.
// Each setting can be switched on and off independently and the rules are enforced on the server.
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

    private async Task<Session> RegisterAsync(string prefix = "pv")
    {
        var username = prefix + "_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Password123" });
        return await LoginAsync(username, "Password123");
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
        SendAsync(HttpMethod.Post, "/api/v1/messages", from, new { recipientId = to.Id, content = "hello" });

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

    // Ratings are written directly to the database, without TMDb.
    private async Task SeedRatingAsync(string userId, int movieId, int rating)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Movies.AnyAsync(m => m.Id == movieId))
        {
            db.Movies.Add(new Movie { Id = movieId, Title = $"Movie {movieId}" });
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
    public async Task Defaults_MatchPreviousBehaviour()
    {
        var me = await RegisterAsync();
        var res = await SendAsync(HttpMethod.Get, "/api/v1/account/privacy", me);
        var json = await JsonAsync(res);

        Assert.Equal("Public", json.GetProperty("profileVisibility").GetString());
        Assert.Equal("Everyone", json.GetProperty("messagePermission").GetString());
        Assert.False(json.GetProperty("hiddenFromSearch").GetBoolean());
    }

    [Fact]
    public async Task Settings_AreSavedAndReadBack()
    {
        var me = await RegisterAsync();
        await SetPrivacyAsync(me, "Following", "Nobody", hidden: true);

        var json = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/account/privacy", me));
        Assert.Equal("Following", json.GetProperty("profileVisibility").GetString());
        Assert.Equal("Nobody", json.GetProperty("messagePermission").GetString());
        Assert.True(json.GetProperty("hiddenFromSearch").GetBoolean());
    }

    [Theory]
    [InlineData("{\"profileVisibility\":\"Everybody\",\"messagePermission\":\"Everyone\"}")]
    [InlineData("{\"profileVisibility\":7,\"messagePermission\":\"Everyone\"}")]
    [InlineData("{\"messagePermission\":\"Everyone\"}")]
    [InlineData("{\"profileVisibility\":\"Public\"}")]
    public async Task InvalidSetting_400(string body)
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
    public async Task PrivateProfile_DetailsOnlyForOwner()
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

        // Everything derived from ratings is closed too.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{owner.Id}", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{owner.Id}/followers", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{owner.Id}/following", viewer)).StatusCode);

        // The owner and an admin see everything.
        var own = await ProfileAsync(owner, owner);
        Assert.False(own.GetProperty("isRestricted").GetBoolean());
        Assert.Equal(1, own.GetProperty("statistics").GetProperty("totalRatings").GetInt32());

        var admin = await LoginAsync(TestWebAppFactory.AdminUserName, TestWebAppFactory.AdminPassword);
        Assert.False((await ProfileAsync(admin, owner)).GetProperty("isRestricted").GetBoolean());

        // Switching it off opens it again.
        await SetPrivacyAsync(owner, "Public");
        var again = await ProfileAsync(viewer, owner);
        Assert.False(again.GetProperty("isRestricted").GetBoolean());
        Assert.Single(again.GetProperty("ratings").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{owner.Id}", viewer)).StatusCode);
    }

    [Fact]
    public async Task FollowingOnly_OwnersFollowDecides()
    {
        var owner = await RegisterAsync();
        var viewer = await RegisterAsync();
        await SetPrivacyAsync(owner, "Following");

        Assert.True((await ProfileAsync(viewer, owner)).GetProperty("isRestricted").GetBoolean());

        // The viewer following the owner is not enough (following needs no approval).
        Assert.Equal(HttpStatusCode.OK, (await FollowAsync(viewer, owner)).StatusCode);
        Assert.True((await ProfileAsync(viewer, owner)).GetProperty("isRestricted").GetBoolean());

        // It opens once the owner follows the viewer.
        Assert.Equal(HttpStatusCode.OK, (await FollowAsync(owner, viewer)).StatusCode);
        Assert.False((await ProfileAsync(viewer, owner)).GetProperty("isRestricted").GetBoolean());
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{owner.Id}/followers", viewer)).StatusCode);
    }

    [Fact]
    public async Task MessagePermission_EveryOption()
    {
        var recipient = await RegisterAsync();
        var sender = await RegisterAsync();

        await SetPrivacyAsync(recipient, messages: "Nobody");
        var nobody = await MessageAsync(sender, recipient);
        Assert.Equal(HttpStatusCode.Forbidden, nobody.StatusCode);
        Assert.Equal("This user does not accept messages.", await ProblemTitleAsync(nobody));

        await SetPrivacyAsync(recipient, messages: "Following");
        var notFollowed = await MessageAsync(sender, recipient);
        Assert.Equal(HttpStatusCode.Forbidden, notFollowed.StatusCode);
        Assert.Equal("This user only accepts messages from people they follow.",
            await ProblemTitleAsync(notFollowed));

        // The sender following the recipient is not enough; the recipient must follow the sender.
        await FollowAsync(sender, recipient);
        Assert.Equal(HttpStatusCode.Forbidden, (await MessageAsync(sender, recipient)).StatusCode);
        await FollowAsync(recipient, sender);
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(sender, recipient)).StatusCode);

        // The restriction only covers messages to the recipient: they can write to anyone.
        await SetPrivacyAsync(recipient, messages: "Nobody");
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(recipient, sender)).StatusCode);

        await SetPrivacyAsync(recipient, messages: "Everyone");
        var stranger = await RegisterAsync();
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(stranger, recipient)).StatusCode);
    }

    [Fact]
    public async Task HiddenFromSearch_SwitchesOnAndOff()
    {
        var target = await RegisterAsync("search");
        var searcher = await RegisterAsync();
        Assert.True(await FindsInSearchAsync(searcher, target));

        await SetPrivacyAsync(target, hidden: true);
        Assert.False(await FindsInSearchAsync(searcher, target));
        // It only hides from search; the profile link keeps working.
        Assert.False((await ProfileAsync(searcher, target)).GetProperty("isRestricted").GetBoolean());

        await SetPrivacyAsync(target, hidden: false);
        Assert.True(await FindsInSearchAsync(searcher, target));
    }

    [Fact]
    public async Task Block_WorksBothWays_EndsWhenRemoved()
    {
        var a = await RegisterAsync("block");
        var b = await RegisterAsync("block");
        await FollowAsync(a, b);
        await FollowAsync(b, a);
        await MessageAsync(b, a);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{b.Id}", a)).StatusCode);
        // Idempotent.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{b.Id}", a)).StatusCode);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/blocks", a));
        Assert.Equal(b.Id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetString());
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/blocks", b))).EnumerateArray());

        // The blocked user cannot see the blocker at all.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/profile/{a.Id}", b)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/follow/{a.Id}/status", b)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{a.Id}", b)).StatusCode);
        // The blocker only sees the identity (so they can unblock).
        var blockedView = await ProfileAsync(a, b);
        Assert.True(blockedView.GetProperty("isRestricted").GetBoolean());
        Assert.True(blockedView.GetProperty("isBlockedByMe").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/compatibility/{b.Id}", a)).StatusCode);

        // Messaging, following and search are closed both ways; follows were deleted.
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

        // The past conversation stays readable.
        var thread = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/messages/thread/{b.Id}", a));
        Assert.Single(thread.EnumerateArray());

        // The side that did not block cannot remove the block; its owner can.
        await SendAsync(HttpMethod.Delete, $"/api/v1/blocks/{a.Id}", b);
        Assert.Equal(HttpStatusCode.Forbidden, (await MessageAsync(b, a)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Delete, $"/api/v1/blocks/{b.Id}", a)).StatusCode);
        Assert.False((await ProfileAsync(b, a)).GetProperty("isRestricted").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await MessageAsync(b, a)).StatusCode);
        Assert.True(await FindsInSearchAsync(a, b));
        // Follows do not come back by themselves.
        var after = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/follow/{b.Id}/status", a));
        Assert.False(after.GetProperty("isFollowing").GetBoolean());
    }

    [Fact]
    public async Task Block_InvalidTargets()
    {
        var me = await RegisterAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{me.Id}", me)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, "/api/v1/blocks/missing-user", me)).StatusCode);
        // Following a missing user used to give a 500 via the FK error.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, "/api/v1/follow/missing-user", me)).StatusCode);
    }

    [Fact]
    public async Task Matching_NeverPutsBlockedPeopleInOneGroup()
    {
        // a and b have identical taste (the most compatible pair), so do c and d; a
        // blocks b. The starting person is random: if the block were ignored, a+b
        // would land in the same group with 1/2 probability at every event.
        var a = await RegisterAsync("match");
        var b = await RegisterAsync("match");
        var c = await RegisterAsync("match");
        var d = await RegisterAsync("match");
        var outsider = await RegisterAsync("match");
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
        // Suggestions without TMDb: a similar user outside the group.
        await SeedRatingAsync(outsider.Id, 920001, 9);
        await SeedRatingAsync(outsider.Id, 920002, 10);

        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{b.Id}", a);

        for (var i = 0; i < 12; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ev = new CinemaEvent { EventDate = DateTime.UtcNow.AddDays(3), LocationName = $"Hall {i}", GroupSize = 2 };
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
            // The block leaves nobody out: a and b each find a place in separate groups.
            Assert.Equal(2, groups.Count);
        }
    }
}
