using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// Global hata yönetiminin davranışını sabitler: iş kuralı hataları doğru durum
// koduyla ve ProblemDetails biçiminde döner, stack trace sızmaz.
public class ErrorHandlingTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ErrorHandlingTests(TestWebAppFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

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

    [Fact]
    public async Task OlmayanEtkinlikteEslestirme_404ProblemDetailsDoner()
    {
        var client = _factory.CreateClient();
        var token = await AdminTokenAsync(client);

        var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/events/999999/create-groups");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        // DomainException.NotFound → 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // ProblemDetails biçimi: güvenli başlık + izleme kimliği.
        Assert.Equal("Etkinlik bulunamadı.", root.GetProperty("title").GetString());
        Assert.True(root.TryGetProperty("traceId", out _), "traceId bekleniyordu");

        // Stack trace / iç ayrıntı sızmamalı.
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body); // .NET stack trace satır biçimi
    }

    [Fact]
    public async Task YetkisizAdminUcu_401Doner()
    {
        // Normal kullanıcı admin ucunu çağıramaz; hata yönetimi bunu da bozmamalı.
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/v1/admin/events/1/create-groups", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
