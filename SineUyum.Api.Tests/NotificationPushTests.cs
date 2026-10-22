using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SineUyum.Api.Hubs;

namespace SineUyum.Api.Tests;

// The notification payload pushed over SignalR must carry the same contract as
// the REST notification and never the user entity (PasswordHash, SecurityStamp,
// email...). The hub context is replaced with a capturing fake; the payload is
// serialized with the web defaults the SignalR JSON protocol uses.
public class NotificationPushTests : IClassFixture<NotificationPushTests.CapturingFactory>
{
    private readonly CapturingFactory _factory;

    public NotificationPushTests(CapturingFactory factory) => _factory = factory;

    public record Pushed(string UserId, string Method, string Json);

    public sealed class CapturingHub : IHubContext<NotificationHub>
    {
        public ConcurrentQueue<Pushed> Sent { get; } = new();

        public IHubClients Clients => new CapturingClients(Sent);
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class CapturingClients(ConcurrentQueue<Pushed> sent) : IHubClients
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        public IClientProxy User(string userId) => new Proxy(sent, userId);

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        // A live chat message is pushed to several users (sender + recipient).
        public IClientProxy Users(IReadOnlyList<string> userIds) => new MultiProxy(userIds.Select(id => new Proxy(sent, id)).ToList());

        private sealed class MultiProxy(List<Proxy> proxies) : IClientProxy
        {
            public async Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                foreach (var p in proxies) await p.SendCoreAsync(method, args, cancellationToken);
            }
        }

        private sealed class Proxy(ConcurrentQueue<Pushed> sent, string userId) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                // Like the real protocol: serialize by the runtime type.
                var json = JsonSerializer.Serialize(args[0], args[0]?.GetType() ?? typeof(object), Web);
                sent.Enqueue(new Pushed(userId, method, json));
                return Task.CompletedTask;
            }
        }
    }

    public class CapturingFactory : TestWebAppFactory
    {
        public CapturingHub Hub { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHubContext<NotificationHub>>();
                services.AddSingleton<IHubContext<NotificationHub>>(Hub);
            });
        }
    }

    private async Task<(string Token, string Id)> RegisterAsync(HttpClient client)
    {
        var username = "push_" + Guid.NewGuid().ToString("N")[..10];
        await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Password123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Password123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("token").GetString()!,
                doc.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = JsonContent.Create(body);
        return req;
    }

    private static void AssertNotificationContract(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "createdAt", "id", "isRead", "message", "relatedUrl" }, keys);
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0, "The notification was pushed before being saved (id=0).");
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MessageNotification_DoesNotLeakUserEntity()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client);
        var recipient = await RegisterAsync(client);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.Id, content = "hello" }));
        res.EnsureSuccessStatusCode();

        var pushed = Assert.Single(_factory.Hub.Sent, p => p.UserId == recipient.Id && p.Method == "ReceiveNotification");
        AssertNotificationContract(pushed.Json);

        // The live chat message (M11) also goes to both sides and leaks no user entity.
        Assert.Contains(_factory.Hub.Sent, p => p.UserId == recipient.Id && p.Method == "ReceiveMessage");
        Assert.Contains(_factory.Hub.Sent, p => p.UserId == sender.Id && p.Method == "ReceiveMessage");
        foreach (var p in _factory.Hub.Sent)
        {
            Assert.DoesNotContain("passwordHash", p.Json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("securityStamp", p.Json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FollowNotification_MatchesContract()
    {
        var client = _factory.CreateClient();
        var follower = await RegisterAsync(client);
        var followed = await RegisterAsync(client);

        var res = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/follow/{followed.Id}", follower.Token));
        res.EnsureSuccessStatusCode();

        var pushed = Assert.Single(_factory.Hub.Sent, p => p.UserId == followed.Id && p.Method == "ReceiveNotification");
        AssertNotificationContract(pushed.Json);
    }
}
