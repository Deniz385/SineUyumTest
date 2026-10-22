using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Localization;
using SineUyum.Api.RateLimiting;

namespace SineUyum.Api.Tests;

// G5: only the login endpoints used to be limited. Now a global limit applies to
// every request, plus a strict limit on TMDb proxies and notification-creating writes.
// Every test starts with its own factory: limit counters do not leak between tests.
public class RateLimitTests
{
    private const int Global = 20;
    private const int Tmdb = 3;
    private const int SocialWrite = 3;

    // With remoteIp, all requests count as coming from that address (e.g. a reverse proxy).
    private sealed class LimitedFactory(string? trustedProxies = null, string? remoteIp = null) : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:GlobalPermitLimit"] = Global.ToString(),
                ["RateLimit:TmdbPermitLimit"] = Tmdb.ToString(),
                ["RateLimit:SocialWritePermitLimit"] = SocialWrite.ToString(),
                ["TRUSTED_PROXIES"] = trustedProxies,
            }));
            if (remoteIp != null)
            {
                builder.ConfigureServices(services =>
                    services.AddSingleton<IStartupFilter>(new RemoteIpFilter(IPAddress.Parse(remoteIp))));
            }
        }
    }

    // Sets the connection's remote address before all of the app's middleware.
    private sealed class RemoteIpFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private static Task<HttpResponseMessage> AnonymousFromAsync(HttpClient client, string forwardedFor)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notification");
        req.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(req);
    }

    private record Session(string Token, string Id);

    private static async Task<Session> RegisterAsync(HttpClient client)
    {
        var username = "rl_" + Guid.NewGuid().ToString("N")[..10];
        await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Password123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Password123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new Session(
            doc.RootElement.GetProperty("token").GetString()!,
            doc.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, Session? s, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (s != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        if (body != null) req.Content = JsonContent.Create(body);
        return client.SendAsync(req);
    }

    private static async Task AssertRejectedAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(LocaleCatalog.Shared.Translate("en", RateLimitingSetup.RejectedTitleKey), doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
        // When it can be retried (seconds; the window is 1 minute).
        var retryAfter = int.Parse(Assert.Single(res.Headers.GetValues("Retry-After")));
        Assert.InRange(retryAfter, 1, 60);
    }

    [Fact]
    public async Task GlobalLimit_PerUser_OtherUserUnaffected()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var bob = await RegisterAsync(client);
        var alice = await RegisterAsync(client);

        for (var i = 0; i < Global; i++)
        {
            var ok = await SendAsync(client, HttpMethod.Get, "/api/v1/notification", bob);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Get, "/api/v1/notification", bob));
        // Any endpoint: the limit covers the whole API.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await SendAsync(client, HttpMethod.Get, "/api/v1/watchlist", bob)).StatusCode);

        // Another user on the same IP uses their own allowance.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", alice)).StatusCode);
    }

    [Fact]
    public async Task GlobalLimit_AnonymousRequests_PerIp()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var bob = await RegisterAsync(client); // 2 anonymous requests (register + login)

        for (var i = 0; i < Global - 2; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", null)).StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Get, "/api/v1/notification", null));
        // Login is anonymous too: it uses the same IP quota (extra brute force protection).
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/api/v1/account/login", new { username = "x", password = "y" })).StatusCode);

        // A signed-in user is independent of the IP quota.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", bob)).StatusCode);
    }

    [Fact]
    public async Task HealthProbe_IsNotLimited()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        for (var i = 0; i < Global + 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        }
    }

    [Fact]
    public async Task TmdbEndpoints_StrictLimit()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var bob = await RegisterAsync(client);

        // No TMDb key: the response may be an error, what matters is that it is not 429.
        for (var i = 0; i < Tmdb; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests,
                (await SendAsync(client, HttpMethod.Get, "/api/v1/movies/popular", bob)).StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Get, "/api/v1/movies/search?query=a", bob));
        // Joint recommendations call TMDb too and share the same quota.
        await AssertRejectedAsync(
            await SendAsync(client, HttpMethod.Get, $"/api/v1/compatibility/{bob.Id}/recommendations", bob));

        // Endpoints other than TMDb keep working under the global limit.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", bob)).StatusCode);
    }

    [Fact]
    public async Task MessagesAndFollows_StrictLimit_ReadsUnaffected()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var bob = await RegisterAsync(client);
        var alice = await RegisterAsync(client);

        for (var i = 0; i < SocialWrite; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/v1/messages", bob,
                new { recipientId = alice.Id, content = $"message {i}" })).StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Post, "/api/v1/messages", bob,
            new { recipientId = alice.Id, content = "too many" }));

        // Reads and the other side are unaffected.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"/api/v1/messages/thread/{alice.Id}", bob)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/v1/messages", alice,
            new { recipientId = bob.Id, content = "reply" })).StatusCode);

        // Following: notification spam via follow/unfollow loops. Same quota as
        // messages (both create notifications), hence a user who has not written yet.
        var carol = await RegisterAsync(client);
        for (var i = 0; i < SocialWrite; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, $"/api/v1/follow/{bob.Id}", carol)).StatusCode);
            await SendAsync(client, HttpMethod.Delete, $"/api/v1/follow/{bob.Id}", carol);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Post, $"/api/v1/follow/{bob.Id}", carol));
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Post, "/api/v1/messages", carol,
            new { recipientId = bob.Id, content = "the follow quota also covers messages" }));
    }

    [Fact]
    public async Task TrustedProxy_LimitsByRealClientIp()
    {
        using var factory = new LimitedFactory(trustedProxies: "10.0.0.0/24", remoteIp: "10.0.0.5");
        var client = factory.CreateClient();

        for (var i = 0; i < Global; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousFromAsync(client, "203.0.113.7")).StatusCode);
        }
        await AssertRejectedAsync(await AnonymousFromAsync(client, "203.0.113.7"));
        // Another client behind the same proxy is unaffected.
        Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousFromAsync(client, "198.51.100.9")).StatusCode);
    }

    [Fact]
    public async Task UntrustedSource_XForwardedForIgnored()
    {
        // No TRUSTED_PROXIES: the client cannot dodge the limit by writing a new IP into the header each time.
        using var factory = new LimitedFactory(remoteIp: "10.0.0.5");
        var client = factory.CreateClient();

        for (var i = 0; i < Global; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousFromAsync(client, $"203.0.113.{i + 1}")).StatusCode);
        }
        await AssertRejectedAsync(await AnonymousFromAsync(client, "198.51.100.9"));
    }

    [Fact]
    public void InvalidProxySetting_FailsAtStartup()
    {
        using var factory = new LimitedFactory(trustedProxies: "10.0.0.0/99");
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("TRUSTED_PROXIES is invalid", error.ToString());
    }
}
