using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SineUyum.Api.Services.Push;

namespace SineUyum.Api.Tests;

// Shared helpers for the push tests.
public static class PushTestSupport
{
    public record Session(string Token, string UserId);

    // language: sent as Accept-Language on register and login (the account's stored language).
    public static async Task<Session> RegisterAsync(HttpClient client, string prefix, string? language = null)
    {
        var username = prefix + Guid.NewGuid().ToString("N")[..10];
        var register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/register")
        {
            Content = JsonContent.Create(new { username, email = $"{username}@test.com", password = "Password123" })
        };
        var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/login")
        {
            Content = JsonContent.Create(new { username, password = "Password123" })
        };
        if (language != null)
        {
            register.Headers.AcceptLanguage.ParseAdd(language);
            login.Headers.AcceptLanguage.ParseAdd(language);
        }
        await client.SendAsync(register);
        var response = await client.SendAsync(login);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new Session(
            doc.RootElement.GetProperty("token").GetString()!,
            doc.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    public static HttpRequestMessage Authed(HttpMethod method, string url, string? token, object? body = null, string? language = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = JsonContent.Create(body);
        if (language != null) req.Headers.AcceptLanguage.ParseAdd(language);
        return req;
    }

    public static async Task RegisterDeviceAsync(HttpClient client, Session s, string deviceToken, string platform = "android")
    {
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/devices", s.Token,
            new { token = deviceToken, platform }));
        res.EnsureSuccessStatusCode();
    }

    // Pushes are sent in the background, so wait until the condition holds.
    public static async Task Eventually(Func<bool> condition, string because, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail($"Timed out: {because}");
            await Task.Delay(25);
        }
    }

    public record Sent(string DeviceToken, PushMessage Message);

    // A fake sender recording what it sends; can answer "invalid token" for
    // specific tokens.
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
