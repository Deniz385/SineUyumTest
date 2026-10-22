using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// M11: live chat — with a real SignalR client, the real hub and a JWT.
public class RealtimeChatTests : IClassFixture<TestWebAppFactory>, IAsyncDisposable
{
    private readonly TestWebAppFactory _factory;
    private readonly List<HubConnection> _connections = new();

    public RealtimeChatTests(TestWebAppFactory factory) => _factory = factory;

    private sealed class Listener
    {
        public ConcurrentQueue<JsonElement> Messages { get; } = new();
        public ConcurrentQueue<JsonElement> Notifications { get; } = new();
    }

    private async Task<Listener> ConnectAsync(Session session)
    {
        var server = _factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/notificationHub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(session.Token);
            })
            .Build();

        var listener = new Listener();
        connection.On<JsonElement>("ReceiveMessage", m => listener.Messages.Enqueue(m));
        connection.On<JsonElement>("ReceiveNotification", n => listener.Notifications.Enqueue(n));
        await connection.StartAsync();
        _connections.Add(connection);
        return listener;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _connections) await c.DisposeAsync();
    }

    private static string[] Keys(JsonElement e) => e.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();

    [Fact]
    public async Task Message_GoesLiveToBothSides_NotToAThirdPerson()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAsync(client, "rt_");
        var bob = await RegisterAsync(client, "rt_");
        var stranger = await RegisterAsync(client, "rt_");

        var aliceLive = await ConnectAsync(alice);
        var bobLive = await ConnectAsync(bob);
        var bobOtherTab = await ConnectAsync(bob); // a second tab of the same account
        var strangerLive = await ConnectAsync(stranger);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", bob.Token,
            new { recipientId = alice.UserId, content = "live hello" }));
        res.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var created = createdDoc.RootElement;
        var id = created.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        Assert.Equal("live hello", created.GetProperty("content").GetString());

        await Eventually(() => aliceLive.Messages.Any() && bobLive.Messages.Any() && bobOtherTab.Messages.Any(),
            "live message to the recipient and all of the sender's connections");

        var received = aliceLive.Messages.Single();
        Assert.Equal(id, received.GetProperty("id").GetInt32());
        Assert.Equal(bob.UserId, received.GetProperty("senderId").GetString());
        Assert.Equal("live hello", received.GetProperty("content").GetString());
        Assert.Equal(id, bobOtherTab.Messages.Single().GetProperty("id").GetInt32());

        // The notification also arrives through the real hub, matching the contract.
        await Eventually(() => aliceLive.Notifications.Any(), "notification to the recipient");
        Assert.Equal(new[] { "createdAt", "id", "isRead", "message", "relatedUrl" },
            Keys(aliceLive.Notifications.Single()));

        // A user outside the conversation receives nothing.
        await Task.Delay(300);
        Assert.Empty(strangerLive.Messages);
        Assert.Empty(strangerLive.Notifications);
    }

    // The client reads the live message with the same model as the thread's.
    [Fact]
    public async Task LiveMessage_SameShapeAsThread()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAsync(client, "rt_");
        var bob = await RegisterAsync(client, "rt_");
        var aliceLive = await ConnectAsync(alice);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", bob.Token,
            new { recipientId = alice.UserId, content = "shape" }));
        res.EnsureSuccessStatusCode();
        await Eventually(() => aliceLive.Messages.Any(), "live message");

        var thread = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/messages/thread/{bob.UserId}", alice.Token));
        using var threadDoc = JsonDocument.Parse(await thread.Content.ReadAsStringAsync());
        var fromThread = threadDoc.RootElement.EnumerateArray().Single();

        Assert.Equal(Keys(fromThread), Keys(aliceLive.Messages.Single()));
        using var createdDoc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(Keys(fromThread), Keys(createdDoc.RootElement));
    }

    // Browser and Flutter clients send the token as a query parameter
    // (WebSockets cannot carry headers); the .NET client uses a header, so this path
    // is tested directly.
    [Fact]
    public async Task Hub_RequiresIdentity_AcceptsQueryParameter()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "rt_");

        var anonymous = await client.PostAsync("/notificationHub/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var withQueryToken = await client.PostAsync(
            $"/notificationHub/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(me.Token)}", null);
        Assert.Equal(HttpStatusCode.OK, withQueryToken.StatusCode);
    }
}
