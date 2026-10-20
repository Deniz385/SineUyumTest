using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.RateLimiting;

namespace SineUyum.Api.Tests;

// G5: önceden yalnızca giriş uçları sınırlıydı. Artık her isteğe genel bir
// sınır, TMDb vekillerine ve bildirim üreten yazmalara ayrıca sıkı sınır.
// Her test kendi fabrikasıyla başlar: sınır sayaçları testler arasında taşmaz.
public class RateLimitTests
{
    private const int Global = 20;
    private const int Tmdb = 3;
    private const int SocialWrite = 3;

    // remoteIp verilirse tüm istekler o adresten (ör. bir ters vekilden) gelmiş sayılır.
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

    // Uygulamanın tüm ara katmanlarından önce bağlantının uzak adresini ayarlar.
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
            new { username, email = $"{username}@test.com", password = "Sifre123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login", new { username, password = "Sifre123" });
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
        Assert.Equal(RateLimitingSetup.RejectedTitle, doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
        // Ne zaman tekrar denenebileceği (saniye, pencere 1 dk).
        var retryAfter = int.Parse(Assert.Single(res.Headers.GetValues("Retry-After")));
        Assert.InRange(retryAfter, 1, 60);
    }

    [Fact]
    public async Task GenelSinir_KullaniciBasina_DigerKullaniciEtkilenmez()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var ali = await RegisterAsync(client);
        var ayse = await RegisterAsync(client);

        for (var i = 0; i < Global; i++)
        {
            var ok = await SendAsync(client, HttpMethod.Get, "/api/v1/notification", ali);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Get, "/api/v1/notification", ali));
        // Herhangi bir uç: sınır tüm API için.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await SendAsync(client, HttpMethod.Get, "/api/v1/watchlist", ali)).StatusCode);

        // Aynı IP'deki başka kullanıcı kendi hakkını kullanır.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", ayse)).StatusCode);
    }

    [Fact]
    public async Task GenelSinir_KimliksizIstekler_IpBasina()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var ali = await RegisterAsync(client); // kimliksiz 2 istek (kayıt + giriş)

        for (var i = 0; i < Global - 2; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", null)).StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Get, "/api/v1/notification", null));
        // Giriş de kimliksiz: aynı IP kotasından düşer (brute force'a ek koruma).
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/api/v1/account/login", new { username = "x", password = "y" })).StatusCode);

        // Giriş yapmış kullanıcı IP kotasından bağımsız.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", ali)).StatusCode);
    }

    [Fact]
    public async Task SaglikYoklamasi_Sinirlanmaz()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        for (var i = 0; i < Global + 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        }
    }

    [Fact]
    public async Task TmdbUclari_SikiSinir()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var ali = await RegisterAsync(client);

        // TMDb anahtarı yok: yanıt hata olabilir, önemli olan 429 olmaması.
        for (var i = 0; i < Tmdb; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests,
                (await SendAsync(client, HttpMethod.Get, "/api/v1/movies/popular", ali)).StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Get, "/api/v1/movies/search?query=a", ali));
        // Ortak öneri de TMDb'ye gider ve aynı kotayı paylaşır.
        await AssertRejectedAsync(
            await SendAsync(client, HttpMethod.Get, $"/api/v1/compatibility/{ali.Id}/recommendations", ali));

        // TMDb dışındaki uçlar genel sınırla çalışmaya devam eder.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/notification", ali)).StatusCode);
    }

    [Fact]
    public async Task MesajVeTakip_SikiSinir_OkumalarEtkilenmez()
    {
        using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var ali = await RegisterAsync(client);
        var ayse = await RegisterAsync(client);

        for (var i = 0; i < SocialWrite; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/v1/messages", ali,
                new { recipientId = ayse.Id, content = $"mesaj {i}" })).StatusCode);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Post, "/api/v1/messages", ali,
            new { recipientId = ayse.Id, content = "fazla" }));

        // Okuma ve karşı taraf etkilenmez.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"/api/v1/messages/thread/{ayse.Id}", ali)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/v1/messages", ayse,
            new { recipientId = ali.Id, content = "cevap" })).StatusCode);

        // Takip: takip/bırak döngüsüyle bildirim spam'i. Mesajla aynı kota
        // (ikisi de bildirim üretir); bu yüzden henüz yazmamış bir kullanıcı.
        var can = await RegisterAsync(client);
        for (var i = 0; i < SocialWrite; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, $"/api/v1/follow/{ali.Id}", can)).StatusCode);
            await SendAsync(client, HttpMethod.Delete, $"/api/v1/follow/{ali.Id}", can);
        }
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Post, $"/api/v1/follow/{ali.Id}", can));
        await AssertRejectedAsync(await SendAsync(client, HttpMethod.Post, "/api/v1/messages", can,
            new { recipientId = ali.Id, content = "takip kotası mesajı da kapsar" }));
    }

    [Fact]
    public async Task GuvenilenVekil_GercekIstemciIpsiyleSinirlar()
    {
        using var factory = new LimitedFactory(trustedProxies: "10.0.0.0/24", remoteIp: "10.0.0.5");
        var client = factory.CreateClient();

        for (var i = 0; i < Global; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousFromAsync(client, "203.0.113.7")).StatusCode);
        }
        await AssertRejectedAsync(await AnonymousFromAsync(client, "203.0.113.7"));
        // Aynı vekilin arkasındaki başka istemci etkilenmez.
        Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousFromAsync(client, "198.51.100.9")).StatusCode);
    }

    [Fact]
    public async Task GuvenilmeyenKaynak_XForwardedForYokSayilir()
    {
        // TRUSTED_PROXIES yok: istemci başlığa her seferinde başka IP yazarak sınırı aşamaz.
        using var factory = new LimitedFactory(remoteIp: "10.0.0.5");
        var client = factory.CreateClient();

        for (var i = 0; i < Global; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousFromAsync(client, $"203.0.113.{i + 1}")).StatusCode);
        }
        await AssertRejectedAsync(await AnonymousFromAsync(client, "198.51.100.9"));
    }

    [Fact]
    public void GecersizVekilAyari_AcilistaHata()
    {
        using var factory = new LimitedFactory(trustedProxies: "10.0.0.0/99");
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("TRUSTED_PROXIES geçersiz", error.ToString());
    }
}
