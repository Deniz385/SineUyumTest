using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Controllers;
using SineUyum.Api.Data;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// B7: cihaz (FCM token) kaydı.
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
    public async Task Kayit_Tekrarlaninca_TekSatirKalir()
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
    public async Task AyniCihazdaBaskaHesap_KayitYeniKullaniciyaTasinir()
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
    public async Task GecersizIstek_400(string token, string platform)
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "dev_");

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/devices", me.Token,
            new { token, platform }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task KimlikDogrulamasizKayit_401()
    {
        var client = _factory.CreateClient();
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/devices", null,
            new { token = NewToken(), platform = "android" }));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // Oturumu düşmüş cihaz da kaydını silebilmeli.
    [Fact]
    public async Task KayitSilme_KimlikDogrulamaGerektirmez()
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
    public async Task CihazSiniriAsilinca_EnEskiSilinir()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client, "dev_");
        var tokens = Enumerable.Range(0, DevicesController.MaxDevicesPerUser + 2).Select(_ => NewToken()).ToArray();

        foreach (var t in tokens) await RegisterDeviceAsync(client, me, t);

        var remaining = (await DevicesAsync(tokens)).Select(r => r.Token).ToHashSet();
        Assert.Equal(DevicesController.MaxDevicesPerUser, remaining.Count);
        // İlk kaydedilen ikisi en uzun süredir görülmeyenler.
        Assert.DoesNotContain(tokens[0], remaining);
        Assert.DoesNotContain(tokens[1], remaining);
        Assert.Contains(tokens[^1], remaining);
    }
}
