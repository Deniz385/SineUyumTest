using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Tests;

// A user's reviews on their profile: newest first, paged, profile-details rule.
public class ProfileReviewsTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public ProfileReviewsTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id);

    private async Task<Session> RegisterAsync()
    {
        var username = "pr_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register", new { username, email = $"{username}@t.com", password = "Password123" });
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Password123" });
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

    private static int _nextMovie = 997000;

    private async Task<int> ReviewAsync(Session s, string text)
    {
        var id = Interlocked.Increment(ref _nextMovie);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Movies.Add(new Movie { Id = id, Title = $"Movie {id}" });
            await db.SaveChangesAsync();
        }
        (await SendAsync(HttpMethod.Post, "/api/v1/ratings", s, new { movieId = id, rating = 8 })).EnsureSuccessStatusCode();
        (await SendAsync(HttpMethod.Put, $"/api/v1/reviews/movie/{id}", s, new { text, containsSpoilers = false })).EnsureSuccessStatusCode();
        return id;
    }

    private async Task<JsonElement> PageAsync(Session viewer, string userId, string query = "")
    {
        var res = await SendAsync(HttpMethod.Get, $"/api/v1/reviews/user/{userId}{query}", viewer);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task UserReviews_NewestFirst_Paged()
    {
        var author = await RegisterAsync();
        var viewer = await RegisterAsync();
        var first = await ReviewAsync(author, "first");
        var second = await ReviewAsync(author, "second");
        var third = await ReviewAsync(author, "third");
        await ReviewAsync(viewer, "not the author's");

        var page1 = await PageAsync(viewer, author.Id, "?pageSize=2");
        Assert.Equal([third, second], page1.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("movie").GetProperty("id").GetInt32()));
        Assert.True(page1.GetProperty("hasMore").GetBoolean());
        Assert.False(page1.GetProperty("items")[0].GetProperty("isMine").GetBoolean());

        var page2 = await PageAsync(viewer, author.Id, "?pageSize=2&page=2");
        Assert.Equal([first], page2.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("movie").GetProperty("id").GetInt32()));
        Assert.False(page2.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task PrivateProfile403_Block404_OwnerAlwaysSees()
    {
        var author = await RegisterAsync();
        var other = await RegisterAsync();
        var blocked = await RegisterAsync();
        await ReviewAsync(author, "hello");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Single(u => u.Id == author.Id).ProfileVisibility = ProfileVisibility.Private;
            db.UserBlocks.Add(new UserBlock { BlockerId = author.Id, BlockedId = blocked.Id });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, $"/api/v1/reviews/user/{author.Id}", other)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/v1/reviews/user/{author.Id}", blocked)).StatusCode);
        Assert.Single((await PageAsync(author, author.Id)).GetProperty("items").EnumerateArray());
    }
}
