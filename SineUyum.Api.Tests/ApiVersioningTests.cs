using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// API versioning: every endpoint lives under "/api/v1/...". Old unversioned
// "/api/..." requests are routed to v1 by a backward compatibility layer, so
// deployed old clients do not break.
public class ApiVersioningTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ApiVersioningTests(TestWebAppFactory factory) => _factory = factory;

    private record NewUserDto(string Username, string Email, string Password);

    private static NewUserDto NewUser()
    {
        var name = "ver_" + Guid.NewGuid().ToString("N")[..10];
        return new NewUserDto(name, $"{name}@test.com", "Password123");
    }

    [Fact]
    public async Task V1Path_Works()
    {
        var client = _factory.CreateClient();
        var user = NewUser();

        var register = await client.PostAsJsonAsync("/api/v1/account/register",
            new { username = user.Username, email = user.Email, password = user.Password });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username = user.Username, password = user.Password });
        login.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task UnversionedPath_IsRoutedToV1()
    {
        var client = _factory.CreateClient();
        var user = NewUser();

        // Register with v1, log in with the old unversioned path — it must work
        // because the compatibility layer moves the request to v1.
        var register = await client.PostAsJsonAsync("/api/v1/account/register",
            new { username = user.Username, email = user.Email, password = user.Password });
        register.EnsureSuccessStatusCode();

        var legacyLogin = await client.PostAsJsonAsync("/api/account/login",
            new { username = user.Username, password = user.Password });

        Assert.Equal(HttpStatusCode.OK, legacyLogin.StatusCode);
    }
}
