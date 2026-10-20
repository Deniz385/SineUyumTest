using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SineUyum.Api.Tests;

// Kimlik doğrulama akışının uçtan uca davranışı: kayıt, giriş, token yenileme
// (rotation), /me ve parola değiştirme. Bunlar daha önce yalnızca elle
// doğrulanıyordu; artık CI'da otomatik koşuyor.
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

    // Her test benzersiz bir kullanıcı adı kullansın ki paylaşılan veritabanında
    // birbirlerini etkilemesinler.
    private static string UniqueName() => "kullanici_" + Guid.NewGuid().ToString("N")[..10];

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
    public async Task Health_Erisilebilir()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Kayit_Ve_Giris_TokenCiftiDoner()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, UniqueName(), "Sifre123");

        Assert.False(string.IsNullOrEmpty(auth.Token));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Contains("User", auth.User.Roles);
    }

    [Fact]
    public async Task Giris_YanlisParola_401()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        await RegisterAndLoginAsync(client, username, "Sifre123");

        var response = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "YanlisParola9" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_GecerliTokenle_KullaniciDoner()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        var auth = await RegisterAndLoginAsync(client, username, "Sifre123");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var user = await ReadAsync<UserSummary>(response);
        Assert.Equal(username, user.UserName);
    }

    [Fact]
    public async Task Me_TokensizIstek_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_YeniTokenVerir_EskisiGecersizlenir()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, UniqueName(), "Sifre123");

        var first = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = auth.RefreshToken });
        first.EnsureSuccessStatusCode();
        var refreshed = await ReadAsync<AuthResponse>(first);
        Assert.False(string.IsNullOrEmpty(refreshed.Token));

        // Rotation: aynı refresh token ikinci kez kullanılamaz.
        var replay = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_GecersizToken_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = "tamamen-uydurma-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ParolaDegistir_EskisiGecersiz_YenisiGecerli()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        var auth = await RegisterAndLoginAsync(client, username, "Sifre123");

        var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = "Sifre123",
                newPassword = "YeniSifre456"
            })
        };
        change.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var changeResponse = await client.SendAsync(change);
        changeResponse.EnsureSuccessStatusCode();

        // Eski parola artık çalışmamalı.
        var eski = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Sifre123" });
        Assert.Equal(HttpStatusCode.Unauthorized, eski.StatusCode);

        // Yeni parola çalışmalı.
        var yeni = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "YeniSifre456" });
        yeni.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ParolaDegistir_DigerOturumlariIptalEder()
    {
        var client = _factory.CreateClient();
        var username = UniqueName();
        // İki ayrı "cihaz" girişi.
        var cihaz1 = await RegisterAndLoginAsync(client, username, "Sifre123");
        var login2 = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Sifre123" });
        var cihaz2 = await ReadAsync<AuthResponse>(login2);

        // Cihaz1 parolayı değiştirir.
        var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = "Sifre123",
                newPassword = "YeniSifre456"
            })
        };
        change.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cihaz1.Token);
        (await client.SendAsync(change)).EnsureSuccessStatusCode();

        // Cihaz2'nin refresh token'ı iptal edilmiş olmalı.
        var replay = await client.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = cihaz2.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task AdminHesabi_Tohumlanmis_GirisYapabilir()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username = TestWebAppFactory.AdminUserName, password = TestWebAppFactory.AdminPassword });

        login.EnsureSuccessStatusCode();
        var auth = await ReadAsync<AuthResponse>(login);
        Assert.Contains("Admin", auth.User.Roles);
    }
}
