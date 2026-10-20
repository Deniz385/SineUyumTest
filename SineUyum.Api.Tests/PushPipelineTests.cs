using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Services;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// B7: bildirim oluşunca alıcının TÜM cihazlarına push gider; FCM token'ı
// öldü derse kayıt silinir.
public class PushPipelineTests : IClassFixture<FakePushFactory>
{
    private readonly FakePushFactory _factory;

    public PushPipelineTests(FakePushFactory factory) => _factory = factory;

    private static string NewToken() => "fcm-" + Guid.NewGuid().ToString("N");

    private IEnumerable<Sent> SentTo(string userId) => _factory.Push.Sent.Where(s => s.Message.UserId == userId);

    [Fact]
    public async Task Mesaj_AlicininTumCihazlarinaPushGider()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "ps_");
        var recipient = await RegisterAsync(client, "pr_");
        string phone = NewToken(), tablet = NewToken();
        await RegisterDeviceAsync(client, recipient, phone, "android");
        await RegisterDeviceAsync(client, recipient, tablet, "ios");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "gizli içerik" }));
        res.EnsureSuccessStatusCode();

        await Eventually(() => SentTo(recipient.UserId).Count() == 2, "iki cihaza push");
        var sent = SentTo(recipient.UserId).ToList();
        Assert.Equal(new[] { phone, tablet }.OrderBy(x => x), sent.Select(s => s.DeviceToken).OrderBy(x => x));

        var push = sent[0].Message;
        Assert.Equal(NotificationService.PushTitle, push.Title);
        Assert.Contains("size bir mesaj gönderdi", push.Body);
        // Mesaj içeriği push'a (kilit ekranına) yazılmaz.
        Assert.DoesNotContain("gizli içerik", push.Body);
        Assert.Equal($"/messages/{sender.UserId}", push.Data["relatedUrl"]);

        // notificationId, kayıtlı bildirimin gerçek id'si olmalı (tıklanınca okundu işaretlemek için).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Notifications.SingleAsync(n => n.UserId == recipient.UserId);
        Assert.Equal(stored.Id.ToString(), push.Data["notificationId"]);

        // Gönderen kendine push almaz.
        Assert.Empty(SentTo(sender.UserId));
    }

    [Fact]
    public async Task Takip_PushGider()
    {
        var client = _factory.CreateClient();
        var follower = await RegisterAsync(client, "pf_");
        var followed = await RegisterAsync(client, "pt_");
        await RegisterDeviceAsync(client, followed, NewToken());

        var res = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/follow/{followed.UserId}", follower.Token));
        res.EnsureSuccessStatusCode();

        await Eventually(() => SentTo(followed.UserId).Any(), "takip push'u");
        Assert.Equal($"/profile/{follower.UserId}", SentTo(followed.UserId).Single().Message.Data["relatedUrl"]);
    }

    [Fact]
    public async Task OluToken_Silinir_DigerleriKalir()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "ps_");
        var recipient = await RegisterAsync(client, "pr_");
        string dead = NewToken(), alive = NewToken();
        await RegisterDeviceAsync(client, recipient, dead);
        await RegisterDeviceAsync(client, recipient, alive);
        _factory.Push.Unregistered[dead] = true;

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "selam" }));
        res.EnsureSuccessStatusCode();

        async Task<List<string>> TokensAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.DeviceTokens.Where(d => d.UserId == recipient.UserId).Select(d => d.Token).ToListAsync();
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        List<string> tokens;
        while ((tokens = await TokensAsync()).Contains(dead) && DateTime.UtcNow < deadline) await Task.Delay(25);

        Assert.Equal(new[] { alive }, tokens);
    }

    [Fact]
    public async Task CihaziOlmayanKullanici_GonderimYok_HataYok()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "ps_");
        var recipient = await RegisterAsync(client, "pr_");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "selam" }));
        res.EnsureSuccessStatusCode();

        // Arka plan işinin kuyruğu boşaltması için kısa bir süre tanı.
        await Task.Delay(200);
        Assert.Empty(SentTo(recipient.UserId));
    }
}
