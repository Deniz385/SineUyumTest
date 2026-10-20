using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// M5/M6: GetConversations son mesajı veritabanında hesaplar ve sayfalar.
// Doğruluğu (her sohbette doğru son mesaj, doğru sıra) ve sayfalamayı sabitler.
public class ConversationsTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ConversationsTests(TestWebAppFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record Auth(string Token, UserInfo User);
    private record UserInfo(string Id, string UserName);

    private async Task<Auth> RegisterAsync(HttpClient client)
    {
        var username = "conv_" + Guid.NewGuid().ToString("N")[..10];
        await client.PostAsJsonAsync("/api/account/register",
            new { username, email = $"{username}@test.com", password = "Sifre123" });
        var login = await client.PostAsJsonAsync("/api/account/login",
            new { username, password = "Sifre123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return new Auth(
            root.GetProperty("token").GetString()!,
            new UserInfo(
                root.GetProperty("user").GetProperty("id").GetString()!,
                root.GetProperty("user").GetProperty("userName").GetString()!));
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = JsonContent.Create(body);
        return req;
    }

    private async Task SendMessageAsync(HttpClient client, string token, string recipientId, string content)
    {
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/messages", token,
            new { recipientId, content }));
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SonMesaj_VeSira_Dogru()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client);
        var ali = await RegisterAsync(client);
        var ayse = await RegisterAsync(client);

        // me -> ali, sonra me -> ayse (ayse ile sohbet daha yeni)
        await SendMessageAsync(client, me.Token, ali.User.Id, "ali ilk");
        await SendMessageAsync(client, me.Token, ali.User.Id, "ali son");
        await SendMessageAsync(client, me.Token, ayse.User.Id, "ayse son");

        var res = await client.SendAsync(Authed(HttpMethod.Get, "/api/messages", me.Token));
        res.EnsureSuccessStatusCode();
        var list = JsonSerializer.Deserialize<List<JsonElement>>(
            await res.Content.ReadAsStringAsync(), Json)!;

        Assert.Equal(2, list.Count);
        // En yeni sohbet (ayse) başta olmalı.
        Assert.Equal(ayse.User.Id, list[0].GetProperty("otherUserId").GetString());
        Assert.Equal("ayse son", list[0].GetProperty("lastMessageContent").GetString());
        // ali sohbetinde son mesaj "ali son" olmalı (ilk değil).
        Assert.Equal(ali.User.Id, list[1].GetProperty("otherUserId").GetString());
        Assert.Equal("ali son", list[1].GetProperty("lastMessageContent").GetString());
    }

    [Fact]
    public async Task Sayfalama_SayfaBasinaSinirlar()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client);

        // 3 farklı kişiyle sohbet.
        for (var i = 0; i < 3; i++)
        {
            var partner = await RegisterAsync(client);
            await SendMessageAsync(client, me.Token, partner.User.Id, $"selam {i}");
        }

        var page1 = await client.SendAsync(
            Authed(HttpMethod.Get, "/api/messages?page=1&pageSize=2", me.Token));
        page1.EnsureSuccessStatusCode();
        var list1 = JsonSerializer.Deserialize<List<JsonElement>>(
            await page1.Content.ReadAsStringAsync(), Json)!;
        Assert.Equal(2, list1.Count);

        var page2 = await client.SendAsync(
            Authed(HttpMethod.Get, "/api/messages?page=2&pageSize=2", me.Token));
        page2.EnsureSuccessStatusCode();
        var list2 = JsonSerializer.Deserialize<List<JsonElement>>(
            await page2.Content.ReadAsStringAsync(), Json)!;
        Assert.Single(list2);

        // Sayfalar çakışmamalı.
        var id1 = list1.Select(x => x.GetProperty("otherUserId").GetString()).ToHashSet();
        var id2 = list2.Select(x => x.GetProperty("otherUserId").GetString()).ToHashSet();
        Assert.Empty(id1.Intersect(id2));
    }
}
