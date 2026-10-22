using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Services.Payments;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// G4: subscriptions without payment were closed; a subscription only opens via a
// payment verified by the provider or an admin; paid features are checked on the server.
public class SubscriptionTests : IClassFixture<SubscriptionTests.Factory>
{
    private readonly Factory _factory;

    public SubscriptionTests(Factory factory) => _factory = factory;

    // A fake provider. Token: "ok:{reference}:{days}:{renews 1/0}".
    // Webhook: X-Fake-Signature: valid, body {reference, kind, periodEnd}.
    public sealed class FakeProvider : IPaymentProvider
    {
        public string Name => "fake";

        public Task<VerifiedPurchase> VerifyAsync(string purchaseToken, string userId, CancellationToken ct)
        {
            var parts = purchaseToken.Split(':');
            if (parts.Length != 4 || parts[0] != "ok") throw new DomainException("test.paymentNotVerified");
            return Task.FromResult(new VerifiedPurchase(Name, parts[1],
                DateTime.UtcNow.AddDays(int.Parse(parts[2])), parts[3] == "1"));
        }

        public async Task<SubscriptionEvent?> ParseWebhookAsync(HttpRequest request, CancellationToken ct)
        {
            if (request.Headers["X-Fake-Signature"] != "valid")
                throw new DomainException("test.invalidSignature", StatusCodes.Status401Unauthorized);
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, cancellationToken: ct);
            var kind = Enum.Parse<SubscriptionEventKind>(body.GetProperty("kind").GetString()!);
            DateTime? end = body.TryGetProperty("periodEnd", out var p) ? p.GetDateTime() : null;
            return new SubscriptionEvent(Name, body.GetProperty("reference").GetString()!, kind, end);
        }
    }

    public class Factory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddSingleton<IPaymentProvider, FakeProvider>());
        }
    }

    private async Task<string> AdminTokenAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username = TestWebAppFactory.AdminUserName, password = TestWebAppFactory.AdminPassword });
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> StatusAsync(HttpClient client, Session s)
    {
        var res = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/subscription", s.Token));
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, Session s, string token, string provider = "fake") =>
        client.SendAsync(Authed(HttpMethod.Post, "/api/v1/subscription/verify", s.Token,
            new { provider, purchaseToken = token }));

    private static string Ref() => "ref-" + Guid.NewGuid().ToString("N")[..10];

    private async Task<int> FutureEventAsync(HttpClient client, int daysAhead = 7)
    {
        var admin = await AdminTokenAsync(client);
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/event", admin,
            new { eventDate = DateTime.UtcNow.AddDays(daysAhead), locationName = "Cinema", groupSize = 2 }));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> WebhookAsync(HttpClient client, object body, string signature = "valid")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscription/webhooks/fake")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("X-Fake-Signature", signature);
        return client.SendAsync(req);
    }

    [Fact]
    public async Task ActivationWithoutPayment_IsGoneAndOpensNothing()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/subscription/activate", me.Token));

        Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        Assert.False((await StatusAsync(client, me)).GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task PaidFeature_IsCheckedOnServer()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");
        var eventId = await FutureEventAsync(client);

        var withoutSub = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/event/{eventId}/join", me.Token));
        Assert.Equal(HttpStatusCode.PaymentRequired, withoutSub.StatusCode);
        var vote = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/event/vote", me.Token, new { groupId = 1, movieId = 1 }));
        Assert.Equal(HttpStatusCode.PaymentRequired, vote.StatusCode);

        (await VerifyAsync(client, me, $"ok:{Ref()}:30:1")).EnsureSuccessStatusCode();

        var withSub = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/event/{eventId}/join", me.Token));
        Assert.Equal(HttpStatusCode.OK, withSub.StatusCode);
    }

    [Fact]
    public async Task VerifiedPayment_OpensSubscription_TokenAndMeUpdated()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");

        var res = await VerifyAsync(client, me, $"ok:{Ref()}:30:1");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("token").GetString()!;

        var claim = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "IsSubscribed").Value;
        Assert.Equal("True", claim);
        Assert.True(doc.RootElement.GetProperty("user").GetProperty("isSubscribed").GetBoolean());

        var status = await StatusAsync(client, me);
        Assert.True(status.GetProperty("isActive").GetBoolean());
        Assert.True(status.GetProperty("willRenew").GetBoolean());
        Assert.Equal("fake", status.GetProperty("provider").GetString());
        Assert.Contains("fake", status.GetProperty("availableProviders").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task SamePayment_CannotOpenAnotherAccount_RetryOnSameAccountIsSafe()
    {
        var client = _factory.CreateClient();
        var owner = await RegisterAsync(client, "sb_");
        var other = await RegisterAsync(client, "sb_");
        var purchase = $"ok:{Ref()}:30:1";

        (await VerifyAsync(client, owner, purchase)).EnsureSuccessStatusCode();
        // The client retried: fine, a single row.
        (await VerifyAsync(client, owner, purchase)).EnsureSuccessStatusCode();

        var replay = await VerifyAsync(client, other, purchase);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.False((await StatusAsync(client, other)).GetProperty("isActive").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Subscriptions.CountAsync(s => s.UserId == owner.UserId));
    }

    [Theory]
    [InlineData("fake", "fake-token", HttpStatusCode.BadRequest)]
    [InlineData("other", "ok:x:30:1", HttpStatusCode.BadRequest)]
    public async Task InvalidPayment_IsRejected(string provider, string token, HttpStatusCode expected)
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");

        var res = await VerifyAsync(client, me, token, provider);

        Assert.Equal(expected, res.StatusCode);
        Assert.False((await StatusAsync(client, me)).GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Webhook_CancelValidUntilPeriodEnd_RefundEndsImmediately()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");
        var reference = Ref();
        (await VerifyAsync(client, me, $"ok:{reference}:30:1")).EnsureSuccessStatusCode();

        (await WebhookAsync(client, new { reference, kind = "Canceled" })).EnsureSuccessStatusCode();
        var canceled = await StatusAsync(client, me);
        Assert.True(canceled.GetProperty("isActive").GetBoolean());
        Assert.False(canceled.GetProperty("willRenew").GetBoolean());

        (await WebhookAsync(client, new { reference, kind = "Refunded" })).EnsureSuccessStatusCode();
        Assert.False((await StatusAsync(client, me)).GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Webhook_RenewalExtendsPeriod()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");
        var reference = Ref();
        (await VerifyAsync(client, me, $"ok:{reference}:1:1")).EnsureSuccessStatusCode();
        var until = DateTime.UtcNow.AddDays(31);

        (await WebhookAsync(client, new { reference, kind = "Renewed", periodEnd = until })).EnsureSuccessStatusCode();

        var expires = (await StatusAsync(client, me)).GetProperty("expiresAt").GetDateTime();
        Assert.True(expires > DateTime.UtcNow.AddDays(30));
    }

    [Fact]
    public async Task Webhook_InvalidSignature401_UnknownProvider404_UnknownPaymentIgnored()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await WebhookAsync(client, new { reference = "x", kind = "Refunded" }, signature: "fake")).StatusCode);

        var unknownProvider = await client.PostAsJsonAsync("/api/v1/subscription/webhooks/missing", new { });
        Assert.Equal(HttpStatusCode.NotFound, unknownProvider.StatusCode);

        // 200 so the provider does not keep resending the same event.
        Assert.Equal(HttpStatusCode.OK,
            (await WebhookAsync(client, new { reference = "unknown", kind = "Refunded" })).StatusCode);
    }

    [Fact]
    public async Task ExpiredSubscription_EndsByItself()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");
        (await VerifyAsync(client, me, $"ok:{Ref()}:30:1")).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.Subscriptions.SingleAsync(s => s.UserId == me.UserId);
            sub.CurrentPeriodEnd = DateTime.UtcNow.AddMinutes(-1); // no renewal arrived
            await db.SaveChangesAsync();
        }

        Assert.False((await StatusAsync(client, me)).GetProperty("isActive").GetBoolean());
        var me2 = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/account/me", me.Token));
        using var doc = JsonDocument.Parse(await me2.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("isSubscribed").GetBoolean());
    }

    [Fact]
    public async Task AdminGrantsManualSubscription_UserCannot()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");
        var admin = await AdminTokenAsync(client);

        var byUser = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/admin/users/{me.UserId}/subscription", me.Token, new { days = 30 }));
        Assert.Equal(HttpStatusCode.Forbidden, byUser.StatusCode);

        var byAdmin = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/admin/users/{me.UserId}/subscription", admin, new { days = 30 }));
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);

        var status = await StatusAsync(client, me);
        Assert.True(status.GetProperty("isActive").GetBoolean());
        Assert.False(status.GetProperty("willRenew").GetBoolean());
        Assert.Equal("manual", status.GetProperty("provider").GetString());
    }

    // G13: existence and date checks when joining an event.
    [Fact]
    public async Task EventJoin_MissingAndPastEventsRejected()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "sb_");
        (await VerifyAsync(client, me, $"ok:{Ref()}:30:1")).EnsureSuccessStatusCode();
        var past = await FutureEventAsync(client, daysAhead: -1);

        var missing = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/event/999999/join", me.Token));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var pastJoin = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/event/{past}/join", me.Token));
        Assert.Equal(HttpStatusCode.BadRequest, pastJoin.StatusCode);
    }
}
