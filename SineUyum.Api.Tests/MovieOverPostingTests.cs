using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SineUyum.Api.Tests;

// G7: /api/v1/ratings/addmovie and /api/v1/ratings must not trust the title/poster
// sent by the client; metadata must come from TMDb. The fake handler returns the
// real TMDb title; the client deliberately sends a wrong one.
public class MovieOverPostingTests : IClassFixture<MovieOverPostingTests.TmdbStubFactory>
{
    private readonly TmdbStubFactory _factory;

    public MovieOverPostingTests(TmdbStubFactory factory) => _factory = factory;

    public sealed class MovieStubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The real title returned by TMDb.
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":550,\"title\":\"Real TMDb Title\",\"poster_path\":\"/real.jpg\"}",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    public class TmdbStubFactory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TMDb:ApiKey"] = "test-tmdb-key",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("TMDb")
                    .ConfigurePrimaryHttpMessageHandler(() => new MovieStubHandler());
            });
        }
    }

    private async Task<string> UserTokenAsync(HttpClient client)
    {
        var username = "op_" + Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Password123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Password123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task AddMovie_IgnoresClientTitle_SavesTmdbTitle()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        // The client deliberately sends a wrong title.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ratings/addmovie")
        {
            Content = JsonContent.Create(new
            {
                id = 550,
                title = "FAKE POISONED TITLE",
                posterPath = "/fake.jpg",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var title = doc.RootElement.GetProperty("title").GetString();

        // The title from TMDb must be saved, NOT the one sent by the client.
        Assert.Equal("Real TMDb Title", title);
        Assert.NotEqual("FAKE POISONED TITLE", title);
    }

    [Fact]
    public async Task Rate_MovieMissingLocally_IsFetchedFromTmdbAndAdded()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        // Rate directly without an addmovie call — the movie must be fetched from TMDb.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ratings")
        {
            Content = JsonContent.Create(new { movieId = 550, rating = 9 }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // Found during M3: adding a movie to a watchlist had the same hole — a movie
    // missing locally was written to Movies with the client's title.
    [Fact]
    public async Task AddToWatchlist_IgnoresClientTitle()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        HttpRequestMessage Authed(HttpMethod m, string url, object? body = null)
        {
            var r = new HttpRequestMessage(m, url);
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) r.Content = JsonContent.Create(body);
            return r;
        }

        var create = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/watchlist", new { name = "List" }));
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var listId = created.RootElement.GetProperty("id").GetInt32();

        // A movie not stored locally, deliberately with a fake title.
        var add = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/watchlist/{listId}/movies",
            new { movieId = 777, title = "FAKE POISONED TITLE", posterPath = "/fake.jpg" }));
        add.EnsureSuccessStatusCode();

        var details = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/watchlist/{listId}"));
        details.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        var item = Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Real TMDb Title", item.GetProperty("title").GetString());
        Assert.Equal("/real.jpg", item.GetProperty("posterPath").GetString());
    }

    // Old clients may not send a title; it is no longer required.
    [Fact]
    public async Task AddToWatchlist_RequestWithoutTitleIsAccepted()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/watchlist")
        {
            Content = JsonContent.Create(new { name = "List" }),
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var created = await client.SendAsync(create);
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var listId = createdDoc.RootElement.GetProperty("id").GetInt32();

        var add = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/watchlist/{listId}/movies")
        {
            Content = JsonContent.Create(new { movieId = 778 }),
        };
        add.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(add);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
