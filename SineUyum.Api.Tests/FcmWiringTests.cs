using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Data;
using SineUyum.Api.Services.Push;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// B7: gerçek Program.cs kurulumunu uçtan uca sınar — Firebase projesi
// olmadan. Test için üretilen bir RSA anahtarıyla servis hesabı JSON'u
// hazırlanır; token_uri yerel bir dinleyiciye işaret eder. Böylece:
//   * Google kütüphanesinin anahtarla imzaladığı OAuth JWT'si doğrulanır
//     (imza, kapsam, hedef),
//   * dönen erişim token'ının FCM isteğinde Bearer olarak kullanıldığı,
//   * FCM HTTP v1 URL'si ve gövdesi,
//   * UNREGISTERED yanıtında kaydın silindiği sınanır.
public class FcmWiringTests : IClassFixture<FcmWiringTests.Factory>
{
    private const string ProjectId = "sineuyum-test-projesi";
    private const string IssuedAccessToken = "ya29.test-erisim-tokeni";

    private readonly Factory _factory;

    public FcmWiringTests(Factory factory) => _factory = factory;

    public record FcmCall(string Url, string? Authorization, JsonElement Body);

    // FCM'i taklit eder; "dead-" ile başlayan token'lar için UNREGISTERED döner.
    private sealed class FakeFcm(ConcurrentQueue<FcmCall> calls) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var body = doc.RootElement.Clone();
            calls.Enqueue(new FcmCall(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));

            var token = body.GetProperty("message").GetProperty("token").GetString()!;
            if (token.StartsWith("dead-"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(
                        "{\"error\":{\"code\":404,\"message\":\"Requested entity was not found.\",\"status\":\"NOT_FOUND\"," +
                        "\"details\":[{\"@type\":\"type.googleapis.com/google.firebase.fcm.v1.FcmError\",\"errorCode\":\"UNREGISTERED\"}]}}",
                        Encoding.UTF8, "application/json"),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"name\":\"projects/{ProjectId}/messages/1\"}}", Encoding.UTF8, "application/json"),
            };
        }
    }

    public class Factory : TestWebAppFactory
    {
        public RSA Key { get; } = RSA.Create(2048);
        public ConcurrentQueue<FcmCall> FcmCalls { get; } = new();
        public ConcurrentQueue<string> OAuthAssertions { get; } = new();
        public string TokenUri { get; }

        private readonly HttpListener _oauth = new();

        public Factory()
        {
            // Boş bir port bul ve Google OAuth token ucunu taklit et.
            var port = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            port.Start();
            var p = ((IPEndPoint)port.LocalEndpoint).Port;
            port.Stop();
            TokenUri = $"http://127.0.0.1:{p}/token";
            _oauth.Prefixes.Add($"http://127.0.0.1:{p}/");
            _oauth.Start();
            _ = Task.Run(ServeOAuthAsync);
        }

        private async Task ServeOAuthAsync()
        {
            while (_oauth.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _oauth.GetContextAsync(); }
                catch { return; }

                using var reader = new StreamReader(ctx.Request.InputStream);
                var form = System.Web.HttpUtility.ParseQueryString(await reader.ReadToEndAsync());
                if (form["grant_type"] == "urn:ietf:params:oauth:grant-type:jwt-bearer" && form["assertion"] is { } assertion)
                {
                    OAuthAssertions.Enqueue(assertion);
                }

                var json = Encoding.UTF8.GetBytes(
                    $"{{\"access_token\":\"{IssuedAccessToken}\",\"expires_in\":3600,\"token_type\":\"Bearer\"}}");
                ctx.Response.ContentType = "application/json";
                await ctx.Response.OutputStream.WriteAsync(json);
                ctx.Response.Close();
            }
        }

        private string ServiceAccountJson() => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = ProjectId,
            ["private_key_id"] = "test-key-id",
            ["private_key"] = Key.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = $"firebase-adminsdk@{ProjectId}.iam.gserviceaccount.com",
            ["client_id"] = "1234567890",
            ["token_uri"] = TokenUri,
        });

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Ortam değişkeni yerine host ayarı: paralel koşan diğer test
            // sınıflarının uygulamasını etkilemesin.
            builder.UseSetting("FCM_SERVICE_ACCOUNT_JSON", ServiceAccountJson());
            builder.ConfigureServices(services =>
                services.AddHttpClient(FcmPushSender.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeFcm(FcmCalls)));
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            // WebApplicationFactory, Dispose → DisposeAsync → Dispose zinciriyle iki kez çağırır.
            if (disposing && !_disposed)
            {
                _disposed = true;
                _oauth.Stop();
                _oauth.Close();
                Key.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task ServisHesabiyla_OAuthVeFcmIstegiDogru()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "fs_");
        var recipient = await RegisterAsync(client, "fr_");
        var deviceToken = "live-" + Guid.NewGuid().ToString("N");
        await RegisterDeviceAsync(client, recipient, deviceToken, "ios");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "selam" }));
        res.EnsureSuccessStatusCode();

        await Eventually(() => _factory.FcmCalls.Any(c => c.Body.GetProperty("message").GetProperty("token").GetString() == deviceToken),
            "FCM'e istek");
        var call = _factory.FcmCalls.First(c => c.Body.GetProperty("message").GetProperty("token").GetString() == deviceToken);

        // FCM HTTP v1 ucu ve OAuth'tan alınan token.
        Assert.Equal($"https://fcm.googleapis.com/v1/projects/{ProjectId}/messages:send", call.Url);
        Assert.Equal($"Bearer {IssuedAccessToken}", call.Authorization);

        var message = call.Body.GetProperty("message");
        Assert.Equal("SineUyum", message.GetProperty("notification").GetProperty("title").GetString());
        Assert.Contains("size bir mesaj gönderdi", message.GetProperty("notification").GetProperty("body").GetString());
        Assert.Equal($"/messages/{sender.UserId}", message.GetProperty("data").GetProperty("relatedUrl").GetString());
        Assert.Equal("high", message.GetProperty("android").GetProperty("priority").GetString());
        Assert.Equal("default", message.GetProperty("apns").GetProperty("payload").GetProperty("aps").GetProperty("sound").GetString());
        // FCM data değerleri string olmalı.
        foreach (var field in message.GetProperty("data").EnumerateObject())
        {
            Assert.Equal(JsonValueKind.String, field.Value.ValueKind);
        }

        // Google kütüphanesinin gönderdiği JWT, servis hesabının anahtarıyla
        // imzalanmış ve FCM kapsamını istiyor olmalı.
        var assertion = Assert.Single(_factory.OAuthAssertions);
        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(assertion, new TokenValidationParameters
        {
            IssuerSigningKey = new RsaSecurityKey(_factory.Key.ExportParameters(false)),
            ValidIssuer = $"firebase-adminsdk@{ProjectId}.iam.gserviceaccount.com",
            // Kütüphane, token_uri farklı olsa da aud olarak Google'ın gerçek
            // token ucunu yazar (Google'ın beklediği budur).
            ValidAudience = "https://oauth2.googleapis.com/token",
        }, out var validated);
        var jwt = (JwtSecurityToken)validated;
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", jwt.Claims.Single(c => c.Type == "scope").Value);
    }

    [Fact]
    public async Task FcmUnregistered_KayitSilinir()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "fs_");
        var recipient = await RegisterAsync(client, "fr_");
        var deadToken = "dead-" + Guid.NewGuid().ToString("N");
        await RegisterDeviceAsync(client, recipient, deadToken);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "selam" }));
        res.EnsureSuccessStatusCode();

        async Task<bool> ExistsAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.DeviceTokens.AnyAsync(d => d.Token == deadToken);
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (await ExistsAsync() && DateTime.UtcNow < deadline) await Task.Delay(25);
        Assert.False(await ExistsAsync(), "UNREGISTERED dönen token silinmeliydi.");
    }
}
