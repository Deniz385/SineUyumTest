using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Tests;

// Public lists: discovery, likes/saves, a user's public lists, saved lists and
// the read-only view, all following the profile-details visibility rule.
public class PublicWatchlistTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public PublicWatchlistTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id, string UserName);

    private async Task<Session> RegisterAsync()
    {
        var username = "pl_" + Guid.NewGuid().ToString("N")[..10];
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

    private async Task<JsonElement> JsonAsync(HttpMethod method, string url, Session s, object? body = null)
    {
        var res = await SendAsync(method, url, s, body);
        Assert.True(res.IsSuccessStatusCode, $"{method} {url}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static int _nextMovie = 990000;

    // A list with `movies` locally stored movies (no TMDb), the latest added last.
    private async Task<int> ListAsync(Session owner, string name, string visibility = "Public", int movies = 3, string? description = null)
    {
        var created = await JsonAsync(HttpMethod.Post, "/api/v1/watchlist", owner, new { name, description, visibility });
        var listId = created.GetProperty("id").GetInt32();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (var i = 0; i < movies; i++)
        {
            var id = Interlocked.Increment(ref _nextMovie);
            db.Movies.Add(new Movie { Id = id, Title = $"Movie {id}", PosterPath = $"/p{id}.jpg" });
            db.WatchlistItems.Add(new WatchlistItem { WatchlistId = listId, MovieId = id, AddedAt = DateTime.UtcNow.AddMinutes(i) });
        }
        await db.SaveChangesAsync();
        return listId;
    }

    private async Task<List<int>> DiscoverAsync(Session viewer, string query = "")
    {
        var page = await JsonAsync(HttpMethod.Get, "/api/v1/watchlist/discover" + query, viewer);
        return page.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();
    }

    private async Task SeedAsync(Action<ApplicationDbContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private Task SetVisibilityAsync(Session s, string visibility) =>
        SendAsync(HttpMethod.Put, "/api/v1/account/privacy", s,
            new { profileVisibility = visibility, messagePermission = "Everyone", hiddenFromSearch = false });

    [Fact]
    public async Task Discover_OnlyOthersPublicListsWithEnoughMovies_PrivateAfterUpdateDisappears()
    {
        var owner = await RegisterAsync();
        var viewer = await RegisterAsync();
        var marker = "disc" + Guid.NewGuid().ToString("N")[..6];
        var shown = await ListAsync(owner, marker + " full");
        var tooShort = await ListAsync(owner, marker + " short", movies: 2);
        var hidden = await ListAsync(owner, marker + " private", visibility: "Private");
        var mine = await ListAsync(viewer, marker + " mine");

        Assert.Equal([shown], await DiscoverAsync(viewer, $"?sort=new&q={marker}"));

        // The owner's own view shows the visibility and counts.
        var lists = await JsonAsync(HttpMethod.Get, "/api/v1/watchlist", owner);
        Assert.Equal("Public", lists.EnumerateArray().Single(l => l.GetProperty("id").GetInt32() == shown).GetProperty("visibility").GetString());

        // Updating without visibility (older clients) keeps it public...
        await JsonAsync(HttpMethod.Put, $"/api/v1/watchlist/{shown}", owner, new { name = marker + " full" });
        Assert.Equal([shown], await DiscoverAsync(viewer, $"?sort=new&q={marker}"));
        // ...making it private removes it.
        await JsonAsync(HttpMethod.Put, $"/api/v1/watchlist/{shown}", owner, new { name = marker + " full", visibility = "Private" });
        Assert.Empty(await DiscoverAsync(viewer, $"?sort=new&q={marker}"));
        _ = (tooShort, hidden, mine);
    }

    // A private list used to be readable by anyone trying sequential ids.
    [Fact]
    public async Task ReadOnlyView_PrivateListOnlyForPeopleItWasSentTo()
    {
        var owner = await RegisterAsync();
        var friend = await RegisterAsync();
        var stranger = await RegisterAsync();
        var listId = await ListAsync(owner, "Secret", visibility: "Private");

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/public/{listId}", stranger)).StatusCode);

        var sent = await SendAsync(HttpMethod.Post, "/api/v1/messages", owner, new { recipientId = friend.Id, watchlistId = listId });
        Assert.True(sent.IsSuccessStatusCode, await sent.Content.ReadAsStringAsync());

        var view = await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/public/{listId}", friend);
        Assert.Equal(3, view.GetProperty("items").GetArrayLength());
        Assert.False(view.GetProperty("canEdit").GetBoolean());
        Assert.Equal(owner.Id, view.GetProperty("owner").GetProperty("id").GetString());
        Assert.True((await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/public/{listId}", owner)).GetProperty("canEdit").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/public/{listId}", stranger)).StatusCode);

        // A public list is readable by anyone who may see the owner.
        await JsonAsync(HttpMethod.Put, $"/api/v1/watchlist/{listId}", owner, new { name = "Secret", visibility = "Public" });
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/public/{listId}", stranger)).StatusCode);
    }

    [Fact]
    public async Task PrivateProfileOrBlock_HidesPublicLists()
    {
        var owner = await RegisterAsync();
        var viewer = await RegisterAsync();
        var marker = "priv" + Guid.NewGuid().ToString("N")[..6];
        var listId = await ListAsync(owner, marker);
        Assert.Equal([listId], await DiscoverAsync(viewer, $"?sort=new&q={marker}"));

        await SetVisibilityAsync(owner, "Private");
        Assert.Empty(await DiscoverAsync(viewer, $"?sort=new&q={marker}"));
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/public/{listId}", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/save", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, $"/api/v1/watchlist/user/{owner.Id}", viewer)).StatusCode);

        await SetVisibilityAsync(owner, "Public");
        await JsonAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/save", viewer);
        await JsonAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/like", viewer);

        // Blocking hides the list and removes the save and like.
        await JsonAsync(HttpMethod.Post, $"/api/v1/blocks/{viewer.Id}", owner);
        Assert.Empty(await DiscoverAsync(viewer, $"?sort=new&q={marker}"));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.WatchlistSaves.AnyAsync(v => v.WatchlistId == listId));
        Assert.False(await db.WatchlistLikes.AnyAsync(l => l.WatchlistId == listId));
    }

    [Fact]
    public async Task LikeAndSave_CountsAndFlags_OwnList400_SaveNotifiesOwnerOnce()
    {
        var owner = await RegisterAsync();
        var fan = await RegisterAsync();
        var listId = await ListAsync(owner, "Weekend picks");

        var like = await JsonAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/like", fan);
        Assert.True(like.GetProperty("liked").GetBoolean());
        Assert.Equal(1, (await JsonAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/like", fan)).GetProperty("likeCount").GetInt32());

        await JsonAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/save", fan);
        await JsonAsync(HttpMethod.Delete, $"/api/v1/watchlist/{listId}/save", fan);
        var saved = await JsonAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/save", fan);
        Assert.Equal(1, saved.GetProperty("saveCount").GetInt32());

        var view = await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/public/{listId}", fan);
        Assert.True(view.GetProperty("likedByMe").GetBoolean());
        Assert.True(view.GetProperty("savedByMe").GetBoolean());
        var detail = await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/{listId}", owner);
        Assert.Equal(1, detail.GetProperty("likeCount").GetInt32());
        Assert.Equal(1, detail.GetProperty("saveCount").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/save", owner)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"/api/v1/watchlist/{listId}/like", owner)).StatusCode);

        // Saving twice (with an unsave in between) notifies once; likes do not notify.
        var notifications = await JsonAsync(HttpMethod.Get, "/api/v1/notification", owner);
        var messages = notifications.EnumerateArray().Select(n => n.GetProperty("message").GetString()).ToList();
        Assert.Equal([$"{fan.UserName} saved your list 'Weekend picks'."], messages);

        var unliked = await JsonAsync(HttpMethod.Delete, $"/api/v1/watchlist/{listId}/like", fan);
        Assert.Equal(0, unliked.GetProperty("likeCount").GetInt32());
    }

    [Fact]
    public async Task Sorts_TrendingUsesTheLastWeek_PopularAllTime_NewByPublishing()
    {
        var owner = await RegisterAsync();
        var viewer = await RegisterAsync();
        var marker = "sort" + Guid.NewGuid().ToString("N")[..6];
        var oldFavorite = await ListAsync(owner, marker + " old");
        var hotNow = await ListAsync(owner, marker + " hot");
        var fresh = await ListAsync(owner, marker + " fresh");
        var fans = new List<Session>();
        for (var i = 0; i < 3; i++) fans.Add(await RegisterAsync());

        await SeedAsync(db =>
        {
            // oldFavorite: 3 saves a month ago; hotNow: 1 save today.
            foreach (var fan in fans)
            {
                db.WatchlistSaves.Add(new WatchlistSave { WatchlistId = oldFavorite, UserId = fan.Id, CreatedAt = DateTime.UtcNow.AddDays(-30) });
            }
            db.WatchlistSaves.Add(new WatchlistSave { WatchlistId = hotNow, UserId = fans[0].Id, CreatedAt = DateTime.UtcNow });
        });

        Assert.Equal([hotNow, oldFavorite, fresh], await DiscoverAsync(viewer, $"?q={marker}"));
        Assert.Equal([oldFavorite, hotNow, fresh], await DiscoverAsync(viewer, $"?sort=popular&q={marker}"));
        Assert.Equal([fresh, hotNow, oldFavorite], await DiscoverAsync(viewer, $"?sort=new&q={marker}"));
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Get, "/api/v1/watchlist/discover?sort=random", viewer)).StatusCode);

        var page1 = await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/discover?sort=new&q={marker}&pageSize=2", viewer);
        Assert.True(page1.GetProperty("hasMore").GetBoolean());
        var page2 = await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/discover?sort=new&q={marker}&pageSize=2&page=2", viewer);
        Assert.False(page2.GetProperty("hasMore").GetBoolean());
        Assert.Equal(oldFavorite, page2.GetProperty("items")[0].GetProperty("id").GetInt32());
    }

    // "For you": lists of people with a similar taste (at least 3 movies in common).
    [Fact]
    public async Task ForYou_ListsOfCompatibleOwnersFirst_WithCompatibilityOnCards()
    {
        var me = await RegisterAsync();
        var twin = await RegisterAsync();
        var opposite = await RegisterAsync();
        var stranger = await RegisterAsync();
        var marker = "fy" + Guid.NewGuid().ToString("N")[..6];
        var twinList = await ListAsync(twin, marker + " twin");
        var oppositeList = await ListAsync(opposite, marker + " opposite");
        var strangerList = await ListAsync(stranger, marker + " stranger");

        var movies = new List<int>();
        await SeedAsync(db =>
        {
            for (var i = 0; i < 4; i++)
            {
                var id = Interlocked.Increment(ref _nextMovie);
                movies.Add(id);
                db.Movies.Add(new Movie { Id = id, Title = $"Rated {id}" });
            }
            int[] mine = [9, 8, 2, 7];
            for (var i = 0; i < movies.Count; i++)
            {
                db.UserRatings.Add(new UserRating { UserId = me.Id, MovieId = movies[i], Rating = mine[i] });
                db.UserRatings.Add(new UserRating { UserId = twin.Id, MovieId = movies[i], Rating = mine[i] });
                db.UserRatings.Add(new UserRating { UserId = opposite.Id, MovieId = movies[i], Rating = 11 - mine[i] });
            }
        });

        Assert.Equal([twinList], await DiscoverAsync(me, $"?sort=forYou&q={marker}"));

        var cards = (await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/discover?sort=new&q={marker}", me)).GetProperty("items");
        var byId = cards.EnumerateArray().ToDictionary(c => c.GetProperty("id").GetInt32());
        Assert.True(byId[twinList].GetProperty("ownerCompatibility").GetDouble() >= 90);
        Assert.Equal(JsonValueKind.Null, byId[strangerList].GetProperty("ownerCompatibility").ValueKind);
        Assert.True(byId[oppositeList].GetProperty("ownerCompatibility").GetDouble() < 60);
    }

    [Fact]
    public async Task Cards_LatestFourPosters_UserListsAndSavedLists()
    {
        var owner = await RegisterAsync();
        var fan = await RegisterAsync();
        var big = await ListAsync(owner, "Big", movies: 6);
        var privateList = await ListAsync(owner, "Mine only", visibility: "Private");

        var profileLists = await JsonAsync(HttpMethod.Get, $"/api/v1/watchlist/user/{owner.Id}", fan);
        var card = Assert.Single(profileLists.GetProperty("items").EnumerateArray());
        Assert.Equal(big, card.GetProperty("id").GetInt32());
        Assert.Equal(6, card.GetProperty("itemCount").GetInt32());
        var posters = card.GetProperty("posters").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Equal(4, posters.Count);
        Assert.Equal($"/p{MovieAddedAt(big, 5)}.jpg", posters[0]);

        await JsonAsync(HttpMethod.Post, $"/api/v1/watchlist/{big}/save", fan);
        var saved = await JsonAsync(HttpMethod.Get, "/api/v1/watchlist/saved", fan);
        Assert.Equal([big], saved.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
        Assert.True(saved.GetProperty("items")[0].GetProperty("savedByMe").GetBoolean());

        // A saved list made private disappears from the saved lists.
        await JsonAsync(HttpMethod.Put, $"/api/v1/watchlist/{big}", owner, new { name = "Big", visibility = "Private" });
        Assert.Empty((await JsonAsync(HttpMethod.Get, "/api/v1/watchlist/saved", fan)).GetProperty("items").EnumerateArray());
        _ = privateList;
    }

    // The movie id of the n-th (0-based) movie added to a list.
    private int MovieAddedAt(int listId, int n)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.WatchlistItems.Where(i => i.WatchlistId == listId).OrderBy(i => i.AddedAt).Skip(n).Select(i => i.MovieId).First();
    }
}
