using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Controllers;
using SineUyum.Api.Data;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// B7: device (FCM token) registration.
public class DevicesTests : IClassFixture<FakePushFactory>
{
    private readonly FakePushFactory _factory;

    public DevicesTests(FakePushFactory factory) => _factory = factory;

    private async Task<List<(string Token, string UserId, string Platform)>> DevicesAsync(params string[] tokens)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.DeviceTokens.Where(d => tokens.Contains(d.Token)).ToListAsync();
        return rows.Select(d => (d.Token, d.UserId, d.Platform)).ToList();
    }

    private static string NewToken() => "fcm-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task RepeatedRegistration_KeepsASingleRow()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "dev_");
        var token = NewToken();

        await RegisterDeviceAsync(client, me, token, "android");
        await RegisterDeviceAsync(client, me, token, "ios");

        var rows = await DevicesAsync(token);
        var row = Assert.Single(rows);
        Assert.Equal(me.UserId, row.UserId);
        Assert.Equal("ios", row.Platform);
    }

    [Fact]
    public async Task OtherAccountOnSameDevice_MovesRowToNewUser()
    {
        var client = _factory.CreateClient();
        var first = await RegisterAsync(client, "dev_");
        var second = await RegisterAsync(client, "dev_");
        var token = NewToken();

        await RegisterDeviceAsync(client, first, token);
        await RegisterDeviceAsync(client, second, token);

        var row = Assert.Single(await DevicesAsync(token));
        Assert.Equal(second.UserId, row.UserId);
    }

    [Theory]
    [InlineData("", "android")]
    [InlineData("tok", "windows")]
    [InlineData("tok", "")]
    public async Task InvalidRequest_400(string token, string platform)
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "dev_");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/devices", me.Token,
            new { token, platform }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRegistration_401()
    {
        var client = _factory.CreateClient();
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/devices", null,
            new { token = NewToken(), platform = "android" }));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // A device whose session is gone must still be able to delete its registration.
    [Fact]
    public async Task Unregister_RequiresNoAuthentication()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "dev_");
        var token = NewToken();
        await RegisterDeviceAsync(client, me, token);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/devices/unregister", null,
            new { token }));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Empty(await DevicesAsync(token));
    }

    [Fact]
    public async Task DeviceLimitExceeded_OldestIsRemoved()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "dev_");
        var tokens = Enumerable.Range(0, DevicesController.MaxDevicesPerUser + 2).Select(_ => NewToken()).ToArray();

        foreach (var t in tokens) await RegisterDeviceAsync(client, me, t);

        var remaining = (await DevicesAsync(tokens)).Select(r => r.Token).ToHashSet();
        Assert.Equal(DevicesController.MaxDevicesPerUser, remaining.Count);
        // The first two registered are the ones not seen for the longest time.
        Assert.DoesNotContain(tokens[0], remaining);
        Assert.DoesNotContain(tokens[1], remaining);
        Assert.Contains(tokens[^1], remaining);
    }
}
