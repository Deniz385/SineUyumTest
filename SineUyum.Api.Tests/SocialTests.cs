using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Tests;

// Social core: reviews, likes, comments and the feed; privacy/block rules.
public class SocialTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public SocialTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id, string UserName);

    private async Task<Session> RegisterAsync()
    {
        var username = "so_" + Guid.NewGuid().ToString("N")[..10];
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

    private static int _nextMovie = 980000;

    // A movie stored locally is rated without calling TMDb.
    private async Task<int> NewMovieAsync()
    {
        var id = Interlocked.Increment(ref _nextMovie);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Movies.Add(new Movie { Id = id, Title = $"Movie {id}", PosterPath = $"/p{id}.jpg" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task RateAsync(Session s, int movieId, int rating)
    {
        var res = await SendAsync(HttpMethod.Post, "/api/v1/ratings", s, new { movieId, rating });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> ReviewAsync(Session s, int movieId, string text, bool spoilers = false)
    {
        var res = await SendAsync(HttpMethod.Put, $"/api/v1/reviews/movie/{movieId}", s, new { text, containsSpoilers = spoilers });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await JsonAsync(res);
    }

    private Task<HttpResponseMessage> FollowAsync(Session from, Session to) =>
        SendAsync(HttpMethod.Post, $"/api/v1/follow/{to.Id}", from);

    private async Task<int> NotificationCountAsync(Session s, string contains)
    {
        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/notification", s));
        return list.EnumerateArray().Count(n => n.GetProperty("message").GetString()!.Contains(contains));
    }

    [Fact]
    public async Task Review_RequiresRating_IsWrittenUpdatedDeleted()
    {
        var me = await RegisterAsync();
        var other = await RegisterAsync();
        var movie = await NewMovieAsync();

        var withoutRating = await SendAsync(HttpMethod.Put, $"/api/v1/reviews/movie/{movie}", me, new { text = "Great" });
        Assert.Equal(HttpStatusCode.BadRequest, withoutRating.StatusCode);
        Assert.Equal("Rate the movie before writing a review.", (await JsonAsync(withoutRating)).GetProperty("title").GetString());

        await RateAsync(me, movie, 9);
        var created = await ReviewAsync(me, movie, "  A really good film.  ", spoilers: true);
        Assert.Equal("A really good film.", created.GetProperty("text").GetString());
        Assert.Equal(9, created.GetProperty("rating").GetInt32());
        Assert.True(created.GetProperty("containsSpoilers").GetBoolean());
        Assert.True(created.GetProperty("isMine").GetBoolean());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("updatedAt").ValueKind);

        var updated = await ReviewAsync(me, movie, "I changed my mind.");
        Assert.Equal(created.GetProperty("id").GetInt32(), updated.GetProperty("id").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, updated.GetProperty("updatedAt").ValueKind);

        // Others see it; the compatibility badge with the author comes from the common rating.
        await RateAsync(other, movie, 9);
        var seen = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{movie}", other));
        var review = Assert.Single(seen.GetProperty("reviews").EnumerateArray());
        Assert.Equal("I changed my mind.", review.GetProperty("text").GetString());
        Assert.False(review.GetProperty("isMine").GetBoolean());
        Assert.Equal(100, review.GetProperty("authorCompatibility").GetDouble());
        Assert.Equal(9, seen.GetProperty("myRating").GetInt32());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("mine").ValueKind);

        var mine = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{movie}", me));
        Assert.Equal("I changed my mind.", mine.GetProperty("mine").GetProperty("text").GetString());
        Assert.Empty(mine.GetProperty("reviews").EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Delete, $"/api/v1/reviews/movie/{movie}", me)).StatusCode);
        var after = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{movie}", other));
        Assert.Equal(0, after.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyReview_400(string text)
    {
        var me = await RegisterAsync();
        var movie = await NewMovieAsync();
        await RateAsync(me, movie, 7);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Put, $"/api/v1/reviews/movie/{movie}", me, new { text })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Put, $"/api/v1/reviews/movie/{movie}", me, new { text = new string('x', 2001) })).StatusCode);
    }

    [Fact]
    public async Task Like_IsIdempotentAndNotifiesOnce_CommentAndDeletePermissions()
    {
        var author = await RegisterAsync();
        var fan = await RegisterAsync();
        var stranger = await RegisterAsync();
        var movie = await NewMovieAsync();
        await RateAsync(author, movie, 8);
        var reviewId = (await ReviewAsync(author, movie, "Watch it")).GetProperty("id").GetInt32();

        var liked = await JsonAsync(await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/like", fan));
        Assert.True(liked.GetProperty("liked").GetBoolean());
        Assert.Equal(1, liked.GetProperty("likeCount").GetInt32());
        // A second like increases neither the count nor the notifications.
        Assert.Equal(1, (await JsonAsync(await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/like", fan))).GetProperty("likeCount").GetInt32());
        Assert.Equal(1, await NotificationCountAsync(author, "liked your review"));

        var view = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{movie}", fan));
        Assert.True(view.GetProperty("reviews")[0].GetProperty("likedByMe").GetBoolean());

        Assert.Equal(0, (await JsonAsync(await SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/like", fan))).GetProperty("likeCount").GetInt32());

        var comment = await JsonAsync(await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", fan, new { text = "I agree!" }));
        var commentId = comment.GetProperty("id").GetInt32();
        Assert.Equal(1, await NotificationCountAsync(author, "commented on your review"));
        // The notification has no comment text.
        Assert.Equal(0, await NotificationCountAsync(author, "I agree"));

        var comments = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", stranger));
        var only = Assert.Single(comments.EnumerateArray());
        Assert.Equal(fan.UserName, only.GetProperty("author").GetProperty("userName").GetString());
        Assert.False(only.GetProperty("canDelete").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Delete, $"/api/v1/reviews/comments/{commentId}", stranger)).StatusCode);
        // The review's owner can delete a comment on their review.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Delete, $"/api/v1/reviews/comments/{commentId}", author)).StatusCode);
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", fan))).EnumerateArray());

        // Liking your own review creates no notification.
        await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/like", author);
        Assert.Equal(1, await NotificationCountAsync(author, "liked your review"));
    }

    [Fact]
    public async Task PrivateAndBlocked_ReviewsNotVisible()
    {
        var author = await RegisterAsync();
        var viewer = await RegisterAsync();
        var commenter = await RegisterAsync();
        var movie = await NewMovieAsync();
        await RateAsync(author, movie, 8);
        var reviewId = (await ReviewAsync(author, movie, "My secret thoughts")).GetProperty("id").GetInt32();
        await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", commenter, new { text = "My comment" });

        await SendAsync(HttpMethod.Put, "/api/v1/account/privacy", author,
            new { profileVisibility = "Private", messagePermission = "Everyone", hiddenFromSearch = false });
        var list = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{movie}", viewer));
        Assert.Empty(list.GetProperty("reviews").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/like", viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", viewer)).StatusCode);

        await SendAsync(HttpMethod.Put, "/api/v1/account/privacy", author,
            new { profileVisibility = "Public", messagePermission = "Everyone", hiddenFromSearch = false });
        Assert.Single((await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{movie}", viewer))).GetProperty("reviews").EnumerateArray());

        // The viewer blocked the commenter: they do not see the comment; the author still does.
        await SendAsync(HttpMethod.Post, $"/api/v1/blocks/{commenter.Id}", viewer);
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", viewer))).EnumerateArray());
        Assert.Single((await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", author))).EnumerateArray());
        // The commenter cannot comment on the review of someone who blocked them.
        await RateAsync(viewer, movie, 6);
        var viewerReview = (await ReviewAsync(viewer, movie, "My review")).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{viewerReview}/comments", commenter, new { text = "hi" })).StatusCode);
    }

    [Fact]
    public async Task Feed_FollowedAndOwn_NewestFirst_CursorPaged()
    {
        var me = await RegisterAsync();
        var friend = await RegisterAsync();
        var other = await RegisterAsync();
        var stranger = await RegisterAsync();
        await FollowAsync(me, friend);
        await FollowAsync(me, other);

        var m1 = await NewMovieAsync();
        var m2 = await NewMovieAsync();
        var m3 = await NewMovieAsync();
        var m4 = await NewMovieAsync();
        await RateAsync(friend, m1, 7);                 // 1
        await RateAsync(other, m2, 9);                  // 2
        await RateAsync(stranger, m3, 10);              // not followed
        await RateAsync(me, m4, 6);                     // 3: my own activity
        await RateAsync(friend, m3, 4);                 // 4
        await ReviewAsync(friend, m1, "Written later"); // m1 moves up
        await RateAsync(friend, m3, 5);                 // a rating change does not move it up

        var feed = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/feed", me));
        var items = feed.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([m1, m3, m4, m2], items.Select(i => i.GetProperty("movie").GetProperty("id").GetInt32()));
        Assert.Equal("Reviewed", items[0].GetProperty("type").GetString());
        Assert.Equal("Written later", items[0].GetProperty("review").GetProperty("text").GetString());
        Assert.Equal("Rated", items[1].GetProperty("type").GetString());
        Assert.Equal(5, items[1].GetProperty("rating").GetInt32());
        Assert.Equal(JsonValueKind.Null, feed.GetProperty("nextCursor").ValueKind);

        // Pagination: 2 per page, no repeats or gaps.
        var seen = new List<int>();
        string? cursor = null;
        do
        {
            var url = "/api/v1/feed?limit=2" + (cursor == null ? "" : $"&cursor={cursor}");
            var page = await JsonAsync(await SendAsync(HttpMethod.Get, url, me));
            seen.AddRange(page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("movie").GetProperty("id").GetInt32()));
            cursor = page.GetProperty("nextCursor").GetString();
        } while (cursor != null);
        Assert.Equal([m1, m3, m4, m2], seen);

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Get, "/api/v1/feed?cursor=broken", me)).StatusCode);

        // A private profile is not shown in the feed either.
        await SendAsync(HttpMethod.Put, "/api/v1/account/privacy", other,
            new { profileVisibility = "Private", messagePermission = "Everyone", hiddenFromSearch = false });
        var hidden = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/feed", me));
        Assert.DoesNotContain(hidden.GetProperty("items").EnumerateArray(), i => i.GetProperty("movie").GetProperty("id").GetInt32() == m2);
    }

    [Fact]
    public async Task OldRatings_NotShownInFeed()
    {
        var me = await RegisterAsync();
        var friend = await RegisterAsync();
        await FollowAsync(me, friend);
        var movie = await NewMovieAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            // A rating given before the feed existed: no timestamps.
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserRatings.Add(new UserRating { UserId = friend.Id, MovieId = movie, Rating = 8 });
            await db.SaveChangesAsync();
        }
        var feed = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/feed", me));
        Assert.Empty(feed.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ScoreForYou_WeightedAverageOfReliableMatchesOnly()
    {
        var me = await RegisterAsync();
        var twin1 = await RegisterAsync();
        var twin2 = await RegisterAsync();
        var opposite = await RegisterAsync();
        var newbie = await RegisterAsync();
        var commons = new[] { await NewMovieAsync(), await NewMovieAsync(), await NewMovieAsync() };
        var target = await NewMovieAsync();

        var taste = new[] { 10, 1, 10 };
        for (var i = 0; i < commons.Length; i++)
        {
            await RateAsync(me, commons[i], taste[i]);
            await RateAsync(twin1, commons[i], taste[i]);
            await RateAsync(twin2, commons[i], taste[i]);
            await RateAsync(opposite, commons[i], 11 - taste[i]);
        }
        await RateAsync(newbie, commons[0], 10); // a single common movie: not reliable

        await RateAsync(twin1, target, 10);
        var single = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{target}", me));
        // A single compatible person is not enough.
        Assert.Equal(JsonValueKind.Null, single.GetProperty("forYou").ValueKind);

        await RateAsync(twin2, target, 8);
        await RateAsync(opposite, target, 2);
        await RateAsync(newbie, target, 1);

        var forYou = (await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{target}", me))).GetProperty("forYou");
        Assert.Equal(9.0, forYou.GetProperty("score").GetDouble());
        Assert.Equal(2, forYou.GetProperty("basedOn").GetInt32());
        var top = forYou.GetProperty("topMatches").EnumerateArray().ToList();
        Assert.Equal(2, top.Count);
        Assert.All(top, t => Assert.Equal(100, t.GetProperty("compatibility").GetDouble()));

        // A private profile does not count: one compatible person remains, no score is shown.
        await SendAsync(HttpMethod.Put, "/api/v1/account/privacy", twin2,
            new { profileVisibility = "Private", messagePermission = "Everyone", hiddenFromSearch = false });
        var hidden = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/reviews/movie/{target}", me));
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("forYou").ValueKind);
    }
}
