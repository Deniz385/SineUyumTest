using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// M10: "Görüldü". Okununca gönderene canlı haber gider, thread'de dateRead
// döner. Okundu bilgisi karşılıklıdır: biri kapatırsa iki taraf da görmez.
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
    public async Task Okununca_GonderenCanliGorur_ThreadDateReadDoner()
    {
        var ali = await RegisterAsync(_client, "rr_");
        var ayse = await RegisterAsync(_client, "rr_");
        var aliReads = await ListenForReadsAsync(ali);
        var ayseReads = await ListenForReadsAsync(ayse);

        await SendAsync(ali, ayse, "bir");
        var last = await SendAsync(ali, ayse, "iki");
        Assert.All(await ThreadAsync(ali, ayse), m => Assert.Equal(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));

        await MarkReadAsync(ayse, ali);

        await Eventually(() => aliReads.Any() && ayseReads.Any(), "gönderene ve okuyanın diğer sekmelerine olay");
        var evt = aliReads.Single();
        Assert.Equal(ayse.UserId, evt.GetProperty("readerId").GetString());
        Assert.Equal(ali.UserId, evt.GetProperty("otherUserId").GetString());
        Assert.Equal(last, evt.GetProperty("lastReadMessageId").GetInt32());

        var thread = await ThreadAsync(ali, ayse);
        Assert.All(thread, m => Assert.NotEqual(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));

        // Yeni okunacak bir şey yokken olay tekrarlanmaz.
        await MarkReadAsync(ayse, ali);
        await Task.Delay(300);
        Assert.Single(aliReads);
    }

    [Theory]
    [InlineData(true)]  // okuyan kapattı: okumasını göstermez
    [InlineData(false)] // gönderen kapattı: başkasınınkini de görmez
    public async Task OkunduBilgisi_Karsilikli_BiriKapatirsaGorunmez(bool readerTurnsOff)
    {
        var ali = await RegisterAsync(_client, "rr_");
        var ayse = await RegisterAsync(_client, "rr_");
        await SetReceiptsAsync(readerTurnsOff ? ayse : ali, false);
        var aliReads = await ListenForReadsAsync(ali);
        var ayseReads = await ListenForReadsAsync(ayse);

        await SendAsync(ali, ayse, "selam");
        await MarkReadAsync(ayse, ali);

        // Okuyanın kendi sekmeleri yine haberdar olur (okunmamış sayısı için).
        await Eventually(() => ayseReads.Any(), "okuyanın diğer sekmesine olay");
        await Task.Delay(300);
        Assert.Empty(aliReads);
        Assert.All(await ThreadAsync(ali, ayse), m => Assert.Equal(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));

        // Tekrar açılınca geçmiş okumalar da görünür (okuma kaydı tutulmaya devam eder).
        await SetReceiptsAsync(readerTurnsOff ? ayse : ali, true);
        Assert.All(await ThreadAsync(ali, ayse), m => Assert.NotEqual(JsonValueKind.Null, m.GetProperty("dateRead").ValueKind));
    }

    [Fact]
    public async Task AyarVarsayilaniAcik_AlanGonderilmezseDegismez()
    {
        var me = await RegisterAsync(_client, "rr_");
        await SetReceiptsAsync(me, false);
        // Bu alanı bilmeyen eski istemci kaydederse ayar korunur.
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
