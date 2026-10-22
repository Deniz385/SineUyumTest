using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Services;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// B7: when a notification is created, a push goes to ALL of the recipient's
// devices; if FCM says a token is dead, the row is deleted.
public class PushPipelineTests : IClassFixture<FakePushFactory>
{
    private readonly FakePushFactory _factory;

    public PushPipelineTests(FakePushFactory factory) => _factory = factory;

    private static string NewToken() => "fcm-" + Guid.NewGuid().ToString("N");

    private IEnumerable<Sent> SentTo(string userId) => _factory.Push.Sent.Where(s => s.Message.UserId == userId);

    [Fact]
    public async Task Message_PushGoesToAllRecipientDevices()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "ps_");
        var recipient = await RegisterAsync(client, "pr_");
        string phone = NewToken(), tablet = NewToken();
        await RegisterDeviceAsync(client, recipient, phone, "android");
        await RegisterDeviceAsync(client, recipient, tablet, "ios");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "secret content" }));
        res.EnsureSuccessStatusCode();

        await Eventually(() => SentTo(recipient.UserId).Count() == 2, "push to two devices");
        var sent = SentTo(recipient.UserId).ToList();
        Assert.Equal(new[] { phone, tablet }.OrderBy(x => x), sent.Select(s => s.DeviceToken).OrderBy(x => x));

        var push = sent[0].Message;
        Assert.Equal(NotificationService.PushTitle, push.Title);
        Assert.Contains("sent you a message", push.Body);
        // The message content is never written into the push (the lock screen).
        Assert.DoesNotContain("secret content", push.Body);
        Assert.Equal($"/messages/{sender.UserId}", push.Data["relatedUrl"]);

        // notificationId must be the stored notification's real id (to mark it read on tap).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Notifications.SingleAsync(n => n.UserId == recipient.UserId);
        Assert.Equal(stored.Id.ToString(), push.Data["notificationId"]);

        // The sender gets no push for themselves.
        Assert.Empty(SentTo(sender.UserId));
    }

    [Fact]
    public async Task Follow_SendsPush()
    {
        var client = _factory.CreateClient();
        var follower = await RegisterAsync(client, "pf_");
        var followed = await RegisterAsync(client, "pt_");
        await RegisterDeviceAsync(client, followed, NewToken());

        var res = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/follow/{followed.UserId}", follower.Token));
        res.EnsureSuccessStatusCode();

        await Eventually(() => SentTo(followed.UserId).Any(), "follow push");
        Assert.Equal($"/profile/{follower.UserId}", SentTo(followed.UserId).Single().Message.Data["relatedUrl"]);
    }

    [Fact]
    public async Task DeadToken_IsDeleted_OthersStay()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "ps_");
        var recipient = await RegisterAsync(client, "pr_");
        string dead = NewToken(), alive = NewToken();
        await RegisterDeviceAsync(client, recipient, dead);
        await RegisterDeviceAsync(client, recipient, alive);
        _factory.Push.Unregistered[dead] = true;

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "hi" }));
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
    public async Task UserWithoutDevices_NoSendNoError()
    {
        var client = _factory.CreateClient();
        var sender = await RegisterAsync(client, "ps_");
        var recipient = await RegisterAsync(client, "pr_");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "hi" }));
        res.EnsureSuccessStatusCode();

        // Give the background job a moment to drain the queue.
        await Task.Delay(200);
        Assert.Empty(SentTo(recipient.UserId));
    }
}
