using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SineUyum.Api.Services.Push;

namespace SineUyum.Api.Tests;

// Push testleri için ortak yardımcılar.
public static class PushTestSupport
{
    public record Session(string Token, string UserId);

    public static async Task<Session> RegisterAsync(HttpClient client, string prefix)
    {
        var username = prefix + Guid.NewGuid().ToString("N")[..10];
        await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Sifre123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Sifre123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new Session(
            doc.RootElement.GetProperty("token").GetString()!,
            doc.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    public static HttpRequestMessage Authed(HttpMethod method, string url, string? token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = JsonContent.Create(body);
        return req;
    }

    public static async Task RegisterDeviceAsync(HttpClient client, Session s, string deviceToken, string platform = "android")
    {
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/devices", s.Token,
            new { token = deviceToken, platform }));
        res.EnsureSuccessStatusCode();
    }

    // Push arka planda gönderildiği için koşul gerçekleşene kadar bekler.
    public static async Task Eventually(Func<bool> condition, string because, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail($"Zaman aşımı: {because}");
            await Task.Delay(25);
        }
    }

    public record Sent(string DeviceToken, PushMessage Message);

    // Gönderimleri kaydeden sahte gönderici; belirli token'lar için
    // "geçersiz token" döndürebilir.
    public sealed class FakePushSender : IPushSender
    {
        public ConcurrentQueue<Sent> Sent { get; } = new();
        public ConcurrentDictionary<string, bool> Unregistered { get; } = new();

        public bool IsEnabled => true;

        public Task<PushSendResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken)
        {
            Sent.Enqueue(new Sent(deviceToken, message));
            return Task.FromResult(Unregistered.ContainsKey(deviceToken)
                ? PushSendResult.InvalidToken
                : PushSendResult.Sent);
        }
    }

    public class FakePushFactory : TestWebAppFactory
    {
        public FakePushSender Push { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushSender>();
                services.AddSingleton<IPushSender>(Push);
            });
        }
    }
}
