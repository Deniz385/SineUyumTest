using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Tests;

// Shared watchlists: invitations, accepting, editing together, "who watched",
// permissions and blocks.
public class SharedWatchlistTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public SharedWatchlistTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id, string UserName);

    private async Task<Session> RegisterAsync()
    {
        var username = "wl_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register", new { username, email = $"{username}@t.com", password = "Password123" });
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Password123" });
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new Session(doc.RootElement.GetProperty("token").GetString()!, doc.RootElement.GetProperty("user").GetProperty("id").GetString()!, username);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session s, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        if (body != null) req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static int _nextMovie = 1_100_000;

    private async Task<int> NewMovieAsync()
    {
        var id = Interlocked.Increment(ref _nextMovie);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Movies.Add(new Movie { Id = id, Title = $"Movie {id}" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<int> CreateListAsync(Session owner, string name = "Weekend")
    {
        var res = await SendAsync(HttpMethod.Post, "/api/v1/watchlist", owner, new { name });
        return (await JsonAsync(res)).GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> AddMovieAsync(Session s, int listId, int movieId) =>
        SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/movies", s, new { movieId });

    private Task<HttpResponseMessage> InviteAsync(Session owner, int listId, Session who) =>
        SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/members", owner, new { userId = who.Id });

    private async Task<List<string>> NotificationsAsync(Session s) =>
        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/notification", s)))
            .EnumerateArray().Select(n => n.GetProperty("message").GetString()!).ToList();

    [Fact]
    public async Task Invite_AcceptedMemberCanCoEdit()
    {
        var owner = await RegisterAsync();
        var friend = await RegisterAsync();
        var listId = await CreateListAsync(owner);
        var m1 = await NewMovieAsync();
        var m2 = await NewMovieAsync();
        Assert.Equal(HttpStatusCode.OK, (await AddMovieAsync(owner, listId, m1)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await InviteAsync(owner, listId, friend)).StatusCode);
        Assert.Contains(await NotificationsAsync(friend), n => n.Contains("invited you to the shared watchlist"));

        // Before accepting they can neither see nor edit it.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", friend)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AddMovieAsync(friend, listId, m2)).StatusCode);
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/watchlist", friend))).EnumerateArray());

        var invitation = Assert.Single((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/watchlist/invitations", friend))).EnumerateArray());
        Assert.Equal(listId, invitation.GetProperty("listId").GetInt32());
        Assert.Equal(owner.UserName, invitation.GetProperty("owner").GetProperty("userName").GetString());

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/accept", friend)).StatusCode);
        Assert.Contains(await NotificationsAsync(owner), n => n.Contains("joined your list"));
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/watchlist/invitations", friend))).EnumerateArray());

        var mine = Assert.Single((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/watchlist", friend))).EnumerateArray());
        Assert.False(mine.GetProperty("isOwner").GetBoolean());
        Assert.True(mine.GetProperty("isShared").GetBoolean());
        Assert.Equal(2, mine.GetProperty("memberCount").GetInt32());
        Assert.Equal(owner.UserName, mine.GetProperty("ownerUsername").GetString());

        // The member adds a movie; the adder is shown. The owner sees the list as shared too.
        Assert.Equal(HttpStatusCode.OK, (await AddMovieAsync(friend, listId, m2)).StatusCode);
        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", owner));
        var added = detail.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("movieId").GetInt32() == m2);
        Assert.Equal(friend.UserName, added.GetProperty("addedByUsername").GetString());
        Assert.Equal(friend.Id, Assert.Single(detail.GetProperty("members").EnumerateArray()).GetProperty("user").GetProperty("id").GetString());
        Assert.True((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/watchlist", owner)))[0].GetProperty("isShared").GetBoolean());

        // A member can remove movies; the same movie cannot be added twice.
        Assert.Equal(HttpStatusCode.BadRequest, (await AddMovieAsync(owner, listId, m2)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Delete, $"/api/v1/watchlist/{listId}/movies/{m1}", friend)).StatusCode);
    }

    [Fact]
    public async Task WhoWatched_FromRatingsOfListPeople()
    {
        var owner = await RegisterAsync();
        var friend = await RegisterAsync();
        var stranger = await RegisterAsync();
        var listId = await CreateListAsync(owner);
        await InviteAsync(owner, listId, friend);
        await SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/accept", friend);

        var both = await NewMovieAsync();
        var onlyFriend = await NewMovieAsync();
        var nobody = await NewMovieAsync();
        foreach (var m in new[] { both, onlyFriend, nobody }) await AddMovieAsync(owner, listId, m);
        foreach (var (who, movie) in new[] { (owner, both), (friend, both), (friend, onlyFriend), (stranger, nobody) })
        {
            (await SendAsync(HttpMethod.Post, "/api/v1/ratings", who, new { movieId = movie, rating = 8 })).EnsureSuccessStatusCode();
        }

        var items = (await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", friend)))
            .GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("movieId").GetInt32(),
                          i => i.GetProperty("watchedBy").EnumerateArray().Select(n => n.GetString()).OrderBy(n => n).ToList());

        Assert.Equal(new[] { owner.UserName, friend.UserName }.OrderBy(n => n), items[both]);
        Assert.Equal([friend.UserName], items[onlyFriend]);
        // Someone outside the list watching it does not count.
        Assert.Empty(items[nobody]);
    }

    [Fact]
    public async Task Permissions_OwnerManages_MemberCanLeave()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var pending = await RegisterAsync();
        var stranger = await RegisterAsync();
        var listId = await CreateListAsync(owner);
        await InviteAsync(owner, listId, member);
        await SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/accept", member);
        await InviteAsync(owner, listId, pending);

        // A pending invitation is only visible to the owner.
        Assert.Equal(2, (await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", owner))).GetProperty("members").GetArrayLength());
        Assert.Single((await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", member))).GetProperty("members").EnumerateArray());

        // A member cannot rename, delete or invite.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, $"/api/v1/watchlist/{listId}", member, new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"/api/v1/watchlist/{listId}", member)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await InviteAsync(member, listId, stranger)).StatusCode);
        // Nor remove someone else.
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Delete, $"/api/v1/watchlist/{listId}/members/{pending.Id}", member)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await InviteAsync(owner, listId, member)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await InviteAsync(owner, listId, owner)).StatusCode);

        // The invitee declines, the member leaves: access ends.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Delete, $"/api/v1/watchlist/{listId}/members/{pending.Id}", pending)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Delete, $"/api/v1/watchlist/{listId}/members/{member.Id}", member)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", member)).StatusCode);
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", owner))).GetProperty("members").EnumerateArray());
    }

    [Fact]
    public async Task Block_PreventsInvitation_EndsMembership()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var blocker = await RegisterAsync();
        var listId = await CreateListAsync(owner);
        await InviteAsync(owner, listId, member);
        await SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/accept", member);

        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{owner.Id}", blocker);
        Assert.Equal(HttpStatusCode.NotFound, (await InviteAsync(owner, listId, blocker)).StatusCode);

        // When the member blocks the owner, the shared list ends.
        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{owner.Id}", member);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", member)).StatusCode);
    }
}
