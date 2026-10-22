using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// Pins down global error handling: business errors come back with the right
// status code as ProblemDetails, titled in the request language, and no
// stack trace leaks.
public class ErrorHandlingTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ErrorHandlingTests(TestWebAppFactory factory) => _factory = factory;

    private async Task<string> AdminTokenAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/v1/account/login", new
        {
            username = TestWebAppFactory.AdminUserName,
            password = TestWebAppFactory.AdminPassword
        });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    [Theory]
    [InlineData(null, "Event not found.")]
    [InlineData("tr", "Etkinlik bulunamadı.")]
    public async Task MatchingForMissingEvent_Returns404ProblemDetails(string? language, string expectedTitle)
    {
        var client = _factory.CreateClient();
        var token = await AdminTokenAsync(client);

        var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/events/999999/create-groups");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (language != null) request.Headers.AcceptLanguage.ParseAdd(language);

        var response = await client.SendAsync(request);

        // DomainException.NotFound → 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // ProblemDetails format: a safe title + a trace id.
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.True(root.TryGetProperty("traceId", out _), "traceId expected");

        // No stack trace / internal details may leak.
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body); // .NET stack trace line format
    }

    [Fact]
    public async Task UnauthorizedAdminEndpoint_Returns401()
    {
        // An anonymous caller cannot call the admin endpoint; error handling must not break that.
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/v1/admin/events/1/create-groups", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
