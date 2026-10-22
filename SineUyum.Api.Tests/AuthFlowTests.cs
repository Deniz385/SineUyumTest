using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// End-to-end behaviour of the authentication flow: register, login, token
// refresh (rotation), /me and password change. These used to be verified only
// by hand; now they run automatically in CI.
public class AuthFlowTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public AuthFlowTests(TestWebAppFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    private record AuthResponse(string Token, string RefreshToken, UserSummary User);
    private record UserSummary(string Id, string UserName, string? Email, List<string> Roles);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    // Every test uses a unique user name so they do not affect each other in
    // the shared database.
    private static string UniqueName() => "user_" + Guid.NewGuid().ToString("N")[..10];

    private async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string username, string password)
    {
        var reg = await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password });
        reg.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password });
        login.EnsureSuccessStatusCode();
        return await ReadAsync<AuthResponse>(login);
    }

    [Fact]
    public async Task Health_IsReachable()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAndLogin_ReturnTokenPair()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, UniqueName(), "Password123");

        Assert.False(string.IsNullOrEmpty(auth.Token));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Contains("User", auth.User.Roles);
    }

    [Fact]
    public async Task Login_WrongPassword_401()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        await RegisterAndLoginAsync(client, username, "Password123");

        var response = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "WrongPassword9" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUser()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        var auth = await RegisterAndLoginAsync(client, username, "Password123");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var user = await ReadAsync<UserSummary>(response);
        Assert.Equal(username, user.UserName);
    }

    [Fact]
    public async Task Me_WithoutToken_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_IssuesNewToken_InvalidatesOldOne()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, UniqueName(), "Password123");

        var first = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = auth.RefreshToken });
        first.EnsureSuccessStatusCode();
        var refreshed = await ReadAsync<AuthResponse>(first);
        Assert.False(string.IsNullOrEmpty(refreshed.Token));

        // Rotation: the same refresh token cannot be used twice.
        var replay = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = "completely-made-up-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_OldStopsWorking_NewWorks()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        var auth = await RegisterAndLoginAsync(client, username, "Password123");

        var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = "Password123",
                newPassword = "NewPassword456"
            })
        };
        change.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var changeResponse = await client.SendAsync(change);
        changeResponse.EnsureSuccessStatusCode();

        // The old password must no longer work.
        var oldLogin = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // The new password must work.
        var newLogin = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "NewPassword456" });
        newLogin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessions()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        // Two separate "device" logins.
        var device1 = await RegisterAndLoginAsync(client, username, "Password123");
        var login2 = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Password123" });
        var device2 = await ReadAsync<AuthResponse>(login2);

        // Device 1 changes the password.
        var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = "Password123",
                newPassword = "NewPassword456"
            })
        };
        change.Headers.Authorization = new AuthenticationHeaderValue("Bearer", device1.Token);
        (await client.SendAsync(change)).EnsureSuccessStatusCode();

        // Device 2's refresh token must have been revoked.
        var replay = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = device2.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task SeededAdminAccount_CanLogIn()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username = TestWebAppFactory.AdminUserName, password = TestWebAppFactory.AdminPassword });

        login.EnsureSuccessStatusCode();
        var auth = await ReadAsync<AuthResponse>(login);
        Assert.Contains("Admin", auth.User.Roles);
    }
}
