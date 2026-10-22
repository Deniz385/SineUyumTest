using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// M5/M6: GetConversations computes the last message in the database and pages.
// Pins down correctness (the right last message per conversation, the right
// order) and pagination.
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
        await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Password123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Password123" });
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
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", token,
            new { recipientId, content }));
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task LastMessage_AndOrder_AreCorrect()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client);
        var bob = await RegisterAsync(client);
        var alice = await RegisterAsync(client);

        // me -> bob, then me -> alice (the conversation with alice is newer)
        await SendMessageAsync(client, me.Token, bob.User.Id, "bob first");
        await SendMessageAsync(client, me.Token, bob.User.Id, "bob last");
        await SendMessageAsync(client, me.Token, alice.User.Id, "alice last");

        var res = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/messages", me.Token));
        res.EnsureSuccessStatusCode();
        var list = JsonSerializer.Deserialize<List<JsonElement>>(
            await res.Content.ReadAsStringAsync(), Json)!;

        Assert.Equal(2, list.Count);
        // The newest conversation (alice) comes first.
        Assert.Equal(alice.User.Id, list[0].GetProperty("otherUserId").GetString());
        Assert.Equal("alice last", list[0].GetProperty("lastMessageContent").GetString());
        // In the conversation with bob the last message is "bob last" (not the first).
        Assert.Equal(bob.User.Id, list[1].GetProperty("otherUserId").GetString());
        Assert.Equal("bob last", list[1].GetProperty("lastMessageContent").GetString());
    }

    [Fact]
    public async Task Pagination_LimitsPerPage()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAsync(client);

        // Conversations with 3 different people.
        for (var i = 0; i < 3; i++)
        {
            var partner = await RegisterAsync(client);
            await SendMessageAsync(client, me.Token, partner.User.Id, $"hi {i}");
        }

        var page1 = await client.SendAsync(
            Authed(HttpMethod.Get, "/api/v1/messages?page=1&pageSize=2", me.Token));
        page1.EnsureSuccessStatusCode();
        var list1 = JsonSerializer.Deserialize<List<JsonElement>>(
            await page1.Content.ReadAsStringAsync(), Json)!;
        Assert.Equal(2, list1.Count);

        var page2 = await client.SendAsync(
            Authed(HttpMethod.Get, "/api/v1/messages?page=2&pageSize=2", me.Token));
        page2.EnsureSuccessStatusCode();
        var list2 = JsonSerializer.Deserialize<List<JsonElement>>(
            await page2.Content.ReadAsStringAsync(), Json)!;
        Assert.Single(list2);

        // Pages must not overlap.
        var id1 = list1.Select(x => x.GetProperty("otherUserId").GetString()).ToHashSet();
        var id2 = list2.Select(x => x.GetProperty("otherUserId").GetString()).ToHashSet();
        Assert.Empty(id1.Intersect(id2));
    }
}
