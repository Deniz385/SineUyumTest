using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// M10: "Seen". On read the sender is told live and the thread returns dateRead.
// Read receipts are mutual: if either side turns them off, neither sees them.
public class ReadReceiptTests : IClassFixture<TestWebAppFactory>, IAsyncDisposable
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;
    private readonly List<HubConnection> _connections = new();

    public ReadReceiptTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _connections) await c.DisposeAsync();
    }

    private async Task<ConcurrentQueue<JsonElement>> ListenForReadsAsync(Session session)
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
        var reads = new ConcurrentQueue<JsonElement>();
        connection.On<JsonElement>("MessagesRead", e => reads.Enqueue(e));
        await connection.StartAsync();
        _connections.Add(connection);
        return reads;
    }

    private async Task<int> SendAsync(Session from, Session to, string content)
    {
        var res = await _client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", from.Token,
            new { recipientId = to.UserId, content }));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("dateRead").ValueKind);
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private async Task MarkReadAsync(Session reader, Session other)
    {
        var res = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/messages/thread/{other.UserId}/mark-as-read", reader.Token));
        res.EnsureSuccessStatusCode();
    }

    private async Task<List<JsonElement>> ThreadAsync(Session viewer, Session other)
    {
        var res = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/messages/thread/{other.UserId}", viewer.Token));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private async Task SetReceiptsAsync(Session s, bool? show)
    {
        var res = await _client.SendAsync(Authed(HttpMethod.Put, "/api/v1/account/privacy", s.Token,
            new { profileVisibility = "Public", messagePermission = "Everyone", hiddenFromSearch = false, showReadReceipts = show }));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task OnRead_SenderSeesItLive_ThreadReturnsDateRead()
    {
        var bob = await RegisterAsync(_client, "rr_");
        var alice = await RegisterAsync(_client, "rr_");
        var bobReads = await ListenForReadsAsync(bob);
        var aliceReads = await ListenForReadsAsync(alice);

        await SendAsync(bob, alice, "one");
        var last = await SendAsync(bob, alice, "two");
        Assert.All(await ThreadAsync(bob, alice), m => Assert.Equal(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));

        await MarkReadAsync(alice, bob);

        await Eventually(() => bobReads.Any() && aliceReads.Any(), "event to the sender and the reader's other tabs");
        var evt = bobReads.Single();
        Assert.Equal(alice.UserId, evt.GetProperty("readerId").GetString());
        Assert.Equal(bob.UserId, evt.GetProperty("otherUserId").GetString());
        Assert.Equal(last, evt.GetProperty("lastReadMessageId").GetInt32());

        var thread = await ThreadAsync(bob, alice);
        Assert.All(thread, m => Assert.NotEqual(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));

        // The event is not repeated when there is nothing new to read.
        await MarkReadAsync(alice, bob);
        await Task.Delay(300);
        Assert.Single(bobReads);
    }

    [Theory]
    [InlineData(true)]  // the reader turned it off: does not show their reads
    [InlineData(false)] // the sender turned it off: does not see anyone else's either
    public async Task ReadReceipts_Mutual_HiddenIfEitherTurnsOff(bool readerTurnsOff)
    {
        var bob = await RegisterAsync(_client, "rr_");
        var alice = await RegisterAsync(_client, "rr_");
        await SetReceiptsAsync(readerTurnsOff ? alice : bob, false);
        var bobReads = await ListenForReadsAsync(bob);
        var aliceReads = await ListenForReadsAsync(alice);

        await SendAsync(bob, alice, "hi");
        await MarkReadAsync(alice, bob);

        // The reader's own tabs are still told (for the unread count).
        await Eventually(() => aliceReads.Any(), "event to the reader's other tab");
        await Task.Delay(300);
        Assert.Empty(bobReads);
        Assert.All(await ThreadAsync(bob, alice), m => Assert.Equal(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));

        // Turning it back on shows past reads too (reads keep being recorded).
        await SetReceiptsAsync(readerTurnsOff ? alice : bob, true);
        Assert.All(await ThreadAsync(bob, alice), m => Assert.NotEqual(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));
    }

    [Fact]
    public async Task SettingDefaultsOn_UnchangedWhenFieldOmitted()
    {
        var me = await RegisterAsync(_client, "rr_");
        await SetReceiptsAsync(me, false);
        // An old client that does not know this field saves: the setting is kept.
        await SetReceiptsAsync(me, null);

        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/account/privacy", me.Token));
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("showReadReceipts").GetBoolean());

        var fresh = await RegisterAsync(_client, "rr_");
        var freshRes = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/account/privacy", fresh.Token));
        using var freshDoc = JsonDocument.Parse(await freshRes.Content.ReadAsStringAsync());
        Assert.True(freshDoc.RootElement.GetProperty("showReadReceipts").GetBoolean());
    }
}
