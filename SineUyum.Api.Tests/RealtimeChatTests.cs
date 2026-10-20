using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// M11: canlı sohbet — gerçek SignalR istemcisiyle, gerçek hub ve JWT ile.
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
    public async Task Mesaj_IkiTarafaCanliGider_UcuncuKisiyeGitmez()
    {
        var client = _factory.CreateClient();
        var ayse = await RegisterAsync(client, "rt_");
        var ali = await RegisterAsync(client, "rt_");
        var baskasi = await RegisterAsync(client, "rt_");

        var ayseLive = await ConnectAsync(ayse);
        var aliLive = await ConnectAsync(ali);
        var aliOtherTab = await ConnectAsync(ali); // aynı hesabın ikinci sekmesi
        var baskasiLive = await ConnectAsync(baskasi);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", ali.Token,
            new { recipientId = ayse.UserId, content = "canlı selam" }));
        res.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var created = createdDoc.RootElement;
        var id = created.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        Assert.Equal("canlı selam", created.GetProperty("content").GetString());

        await Eventually(() => ayseLive.Messages.Any() && aliLive.Messages.Any() && aliOtherTab.Messages.Any(),
            "alıcıya ve gönderenin tüm bağlantılarına canlı mesaj");

        var received = ayseLive.Messages.Single();
        Assert.Equal(id, received.GetProperty("id").GetInt32());
        Assert.Equal(ali.UserId, received.GetProperty("senderId").GetString());
        Assert.Equal("canlı selam", received.GetProperty("content").GetString());
        Assert.Equal(id, aliOtherTab.Messages.Single().GetProperty("id").GetInt32());

        // Bildirim de gerçek hub üzerinden, sözleşmeye uygun gelir.
        await Eventually(() => ayseLive.Notifications.Any(), "alıcıya bildirim");
        Assert.Equal(new[] { "createdAt", "id", "isRead", "message", "relatedUrl" },
            Keys(ayseLive.Notifications.Single()));

        // Konuşmanın dışındaki kullanıcı hiçbir şey almaz.
        await Task.Delay(300);
        Assert.Empty(baskasiLive.Messages);
        Assert.Empty(baskasiLive.Notifications);
    }

    // İstemci canlı mesajı, thread'den gelen mesajla aynı modelle okur.
    [Fact]
    public async Task CanliMesaj_ThreadIleAyniSekilde()
    {
        var client = _factory.CreateClient();
        var ayse = await RegisterAsync(client, "rt_");
        var ali = await RegisterAsync(client, "rt_");
        var ayseLive = await ConnectAsync(ayse);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", ali.Token,
            new { recipientId = ayse.UserId, content = "şekil" }));
        res.EnsureSuccessStatusCode();
        await Eventually(() => ayseLive.Messages.Any(), "canlı mesaj");

        var thread = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/messages/thread/{ali.UserId}", ayse.Token));
        using var threadDoc = JsonDocument.Parse(await thread.Content.ReadAsStringAsync());
        var fromThread = threadDoc.RootElement.EnumerateArray().Single();

        Assert.Equal(Keys(fromThread), Keys(ayseLive.Messages.Single()));
        using var createdDoc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(Keys(fromThread), Keys(createdDoc.RootElement));
    }

    // Tarayıcı ve Flutter istemcileri token'ı sorgu parametresiyle yollar
    // (WebSocket başlık taşıyamaz); .NET istemcisi başlık kullandığı için bu yol
    // doğrudan sınanır.
    [Fact]
    public async Task Hub_KimlikIster_SorguParametresiyleKabulEder()
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
