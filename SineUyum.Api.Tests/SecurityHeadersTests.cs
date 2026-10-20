using System.Net;
using System.Net.Http.Json;
using SineUyum.Api.Middleware;

namespace SineUyum.Api.Tests;

// G9: güvenlik başlıkları her yanıtta — başarılı, kimliksiz, bulunamadı ve
// doğrulama hatası dahil.
public class SecurityHeadersTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(TestWebAppFactory factory) => _client = factory.CreateClient();

    public static TheoryData<string, string, HttpStatusCode> Requests => new()
    {
        { "GET", "/health", HttpStatusCode.OK },
        { "GET", "/api/v1/account/me", HttpStatusCode.Unauthorized },
        { "GET", "/api/v1/yok-boyle-bir-uc", HttpStatusCode.NotFound },
        { "POST", "/api/v1/account/login", HttpStatusCode.BadRequest },
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task HerYanittaGuvenlikBasliklari(string method, string url, HttpStatusCode expected)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST") req.Content = JsonContent.Create(new { });
        var res = await _client.SendAsync(req);

        Assert.Equal(expected, res.StatusCode);
        Assert.Equal("nosniff", Header(res, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(res, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(res, "Referrer-Policy"));
        Assert.Equal(SecurityHeaders.ApiContentSecurityPolicy, Header(res, "Content-Security-Policy"));
        Assert.Contains("camera=()", Header(res, "Permissions-Policy"));
        Assert.False(res.Headers.Contains("Server"), "Server başlığı sunucu yazılımını açık etmemeli");
    }

    private static string? Header(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) ? values.Single()
        : res.Content.Headers.TryGetValues(name, out var contentValues) ? contentValues.Single()
        : null;
}
