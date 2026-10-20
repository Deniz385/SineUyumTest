using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SineUyum.Api.Tests;

// M9: TMDb yanıtlarının önbelleklendiğini doğrular. TMDb'ye giden HTTP, çağrı
// sayan bir sahte handler ile değiştirilir; ikinci istekte sayaç artmamalı.
public class MovieCacheTests : IClassFixture<MovieCacheTests.CachingFactory>
{
    private readonly CachingFactory _factory;

    public MovieCacheTests(CachingFactory factory) => _factory = factory;

    // TMDb'ye kaç kez gidildiğini sayan handler.
    public sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
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
                // "TMDb" adlı istemcinin ağ katmanını sayaç handler ile değiştir.
                services.AddHttpClient("TMDb")
                    .ConfigurePrimaryHttpMessageHandler(() => Handler);
            });
        }
    }

    private async Task<string> UserTokenAsync(HttpClient client)
    {
        var username = "cache_" + Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/account/register",
            new { username, email = $"{username}@test.com", password = "Sifre123" });
        var login = await client.PostAsJsonAsync("/api/account/login",
            new { username, password = "Sifre123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Popüler_IkinciIstek_CachetenGelir_TmdbyeGitmez()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        async Task<HttpResponseMessage> Popular()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/movies/popular");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(req);
        }

        var before = _factory.Handler.CallCount;

        var first = await Popular();
        first.EnsureSuccessStatusCode();
        var second = await Popular();
        second.EnsureSuccessStatusCode();

        // İki istek yapıldı ama TMDb'ye yalnızca bir kez gidilmeli (ikincisi cache).
        Assert.Equal(before + 1, _factory.Handler.CallCount);

        // İçerik de doğru dönmeli.
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("\"results\"", body);
    }
}
