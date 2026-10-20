using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// API sürümleme: tüm uçlar artık "/api/v1/..." altında. Eski sürümsüz
// "/api/..." istekleri ise geriye dönük uyumluluk katmanıyla v1'e yönlenir,
// böylece dağıtılmış eski istemciler kırılmaz.
public class ApiVersioningTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ApiVersioningTests(TestWebAppFactory factory) => _factory = factory;

    private record NewUserDto(string Username, string Email, string Password);

    private static NewUserDto NewUser()
    {
        var name = "ver_" + Guid.NewGuid().ToString("N")[..10];
        return new NewUserDto(name, $"{name}@test.com", "Sifre123");
    }

    [Fact]
    public async Task V1_Yolu_Calisir()
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
    public async Task SurumSuz_Yol_V1e_Yonlenir()
    {
        var client = _factory.CreateClient();
        var user = NewUser();

        // Kayıt v1 ile; giriş eski sürümsüz yol ile — uyumluluk katmanı
        // isteği v1'e taşıdığı için çalışmalı.
        var register = await client.PostAsJsonAsync("/api/v1/account/register",
            new { username = user.Username, email = user.Email, password = user.Password });
        register.EnsureSuccessStatusCode();

        var legacyLogin = await client.PostAsJsonAsync("/api/account/login",
            new { username = user.Username, password = user.Password });

        Assert.Equal(HttpStatusCode.OK, legacyLogin.StatusCode);
    }
}
