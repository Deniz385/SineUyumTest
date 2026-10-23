using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SineUyum.Api.Tests;

// M9: checks that TMDb responses are cached, per language. HTTP to TMDb is
// replaced with a fake handler that counts calls; a repeated request in the
// same language must not increase the count.
public class MovieCacheTests : IClassFixture<MovieCacheTests.CachingFactory>
{
    private readonly CachingFactory _factory;

    public MovieCacheTests(CachingFactory factory) => _factory = factory;

    // Counts how often TMDb is called and records the URLs.
    public sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount;
        public ConcurrentQueue<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            Urls.Enqueue(request.RequestUri!.ToString());
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"results\":[{\"id\":1,\"title\":\"Test\"}]}",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    public class CachingFactory : TestWebAppFactory
    {
        public readonly CountingHandler Handler = new();

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
                // Replace the network layer of the "TMDb" client with the counting handler.
                services.AddHttpClient("TMDb")
                    .ConfigurePrimaryHttpMessageHandler(() => Handler);
            });
        }
    }

    private async Task<string> UserTokenAsync(HttpClient client)
    {
        var username = "cache_" + Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Password123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Password123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Popular_SecondRequestComesFromCache_PerLanguage()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        async Task<HttpResponseMessage> Popular(string? language = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/movies/popular");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (language != null) req.Headers.AcceptLanguage.ParseAdd(language);
            return await client.SendAsync(req);
        }

        var before = _factory.Handler.CallCount;

        var first = await Popular();
        first.EnsureSuccessStatusCode();
        var second = await Popular();
        second.EnsureSuccessStatusCode();

        // Two requests but TMDb is called only once (the second comes from the cache).
        Assert.Equal(before + 1, _factory.Handler.CallCount);
        Assert.Contains("language=en-US", _factory.Handler.Urls.Last());

        // The content must be right too.
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("\"results\"", body);

        // Another language has its own cache entry and asks TMDb in that language.
        (await Popular("tr-TR")).EnsureSuccessStatusCode();
        Assert.Equal(before + 2, _factory.Handler.CallCount);
        Assert.Contains("language=tr-TR", _factory.Handler.Urls.Last());
        (await Popular("tr")).EnsureSuccessStatusCode();
        Assert.Equal(before + 2, _factory.Handler.CallCount);
    }

    // Top rated (first-run welcome): cached per language and page, page clamped.
    [Fact]
    public async Task TopRated_CachedPerPage_PageClamped()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        async Task<HttpResponseMessage> TopRated(string query)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/movies/top_rated" + query);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(req);
        }

        var before = _factory.Handler.CallCount;
        (await TopRated("?page=2")).EnsureSuccessStatusCode();
        (await TopRated("?page=2")).EnsureSuccessStatusCode();
        Assert.Equal(before + 1, _factory.Handler.CallCount);
        Assert.Contains("movie/top_rated", _factory.Handler.Urls.Last());
        Assert.Contains("page=2", _factory.Handler.Urls.Last());

        (await TopRated("?page=99")).EnsureSuccessStatusCode();
        Assert.Contains("page=5", _factory.Handler.Urls.Last());
    }
}
