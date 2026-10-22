using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SineUyum.Api.Data;
using SineUyum.Api.Hubs;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using static SineUyum.Api.Tests.PushTestSupport;

namespace SineUyum.Api.Tests;

// Server-side localization: request culture (Accept-Language / ?lang=, default
// en), locale file parity, localized errors/validation/Identity messages, the
// user's stored language and notifications rendered per language.
public class LocalizationTests : IClassFixture<LocalizationTests.Factory>
{
    // Captures SignalR events and pushes.
    public class Factory : FakePushFactory
    {
        public NotificationPushTests.CapturingHub Hub { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHubContext<NotificationHub>>();
                services.AddSingleton<IHubContext<NotificationHub>>(Hub);
            });
        }
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;

    public LocalizationTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}");

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private async Task<JsonElement> MeAsync(Session s) =>
        await JsonAsync(await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/account/me", s.Token)));

    private static HttpRequestMessage Anonymous(HttpMethod method, string url, object body, string? language)
    {
        var req = Authed(method, url, null, body);
        if (language != null) req.Headers.AcceptLanguage.ParseAdd(language);
        return req;
    }

    [Fact]
    public void LocaleFiles_HaveTheSameKeysAndPlaceholders()
    {
        var catalog = LocaleCatalog.Shared;
        Assert.Contains("en", catalog.Languages);
        Assert.Contains("tr", catalog.Languages);

        var english = catalog.Entries("en");
        foreach (var language in catalog.Languages.Where(l => l != "en"))
        {
            var other = catalog.Entries(language);
            Assert.Empty(english.Keys.Except(other.Keys).Select(k => $"{language} is missing {k}"));
            Assert.Empty(other.Keys.Except(english.Keys).Select(k => $"{language} has an extra key {k}"));

            foreach (var (key, template) in english)
            {
                static string Indices(string text) => string.Join(",",
                    Placeholder.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Distinct().Order());
                Assert.True(Indices(template) == Indices(other[key]),
                    $"Placeholders differ for {key}: en '{template}' vs {language} '{other[key]}'");
            }
        }
    }

    [Fact]
    public void EveryConfiguredLanguageHasALocaleFile()
    {
        var supported = _factory.Services.GetRequiredService<SupportedLanguages>().All;
        Assert.Equal(["en", "tr"], supported);
        Assert.All(supported, language => Assert.Contains(language, LocaleCatalog.Shared.Languages));
    }

    [Fact]
    public void MissingKey_FallsBackToEnglishThenToTheKey()
    {
        var catalog = new LocaleCatalog(new Dictionary<string, string>
        {
            ["en"] = "{\"a\":{\"only\":\"English only {0}\"}}",
            ["tr"] = "{\"a\":{}}",
        });
        Assert.Equal("English only 5", catalog.Translate("tr", "a.only", 5));
        Assert.Equal("a.missing", catalog.Translate("tr", "a.missing"));
        Assert.Equal("English only 5", catalog.Translate("xx", "a.only", 5));
    }

    // The same error in each request language; unsupported or missing → English.
    [Theory]
    [InlineData(null, null, "User not found.")]
    [InlineData("tr", null, "Kullanıcı bulunamadı.")]
    [InlineData("tr-TR", null, "Kullanıcı bulunamadı.")]
    [InlineData("de", null, "User not found.")]
    [InlineData("de, tr;q=0.8", null, "Kullanıcı bulunamadı.")]
    [InlineData("en", "tr", "Kullanıcı bulunamadı.")]
    [InlineData(null, "xx", "User not found.")]
    public async Task Error_IsInTheRequestLanguage(string? acceptLanguage, string? langQuery, string expected)
    {
        var me = await RegisterAsync(_client, "loc_");
        var url = "/api/v1/compatibility/missing-user" + (langQuery == null ? "" : $"?lang={langQuery}");
        var req = Authed(HttpMethod.Get, url, me.Token);
        if (acceptLanguage != null) req.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);

        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(expected, (await JsonAsync(res)).GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("en", "One or more validation errors occurred.", "Username must be at least 3 characters long.")]
    [InlineData("tr", "Bir veya daha fazla doğrulama hatası oluştu.", "Kullanıcı adı en az 3 karakter olmalıdır.")]
    public async Task ValidationMessages_AreLocalized(string language, string title, string message)
    {
        var res = await _client.SendAsync(Anonymous(HttpMethod.Post, "/api/v1/account/register",
            new { username = "ab", email = "ab@test.com", password = "Password123" }, language));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await JsonAsync(res);
        Assert.Equal(title, problem.GetProperty("title").GetString());
        var messages = problem.GetProperty("errors").EnumerateObject()
            .SelectMany(e => e.Value.EnumerateArray().Select(m => m.GetString()));
        Assert.Contains(message, messages);
    }

    [Theory]
    [InlineData("en", "Passwords must have at least one digit ('0'-'9').")]
    [InlineData("tr", "Şifre en az bir rakam ('0'-'9') içermelidir.")]
    public async Task IdentityPasswordErrors_AreLocalized(string language, string expected)
    {
        var username = "loc_" + Guid.NewGuid().ToString("N")[..10];
        var res = await _client.SendAsync(Anonymous(HttpMethod.Post, "/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "nodigits" }, language));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var error = Assert.Single((await JsonAsync(res)).EnumerateArray(),
            e => e.GetProperty("code").GetString() == "PasswordRequiresDigit");
        Assert.Equal(expected, error.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Language_SetAtRegistration_ShownInMe_ChangedWithPut()
    {
        var english = await RegisterAsync(_client, "loc_");
        Assert.Equal("en", (await MeAsync(english)).GetProperty("language").GetString());

        var turkish = await RegisterAsync(_client, "loc_", language: "tr-TR");
        Assert.Equal("tr", (await MeAsync(turkish)).GetProperty("language").GetString());

        var put = await _client.SendAsync(Authed(HttpMethod.Put, "/api/v1/account/language", turkish.Token, new { language = "en" }));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("en", (await JsonAsync(put)).GetProperty("language").GetString());
        Assert.Equal("en", (await MeAsync(turkish)).GetProperty("language").GetString());

        // Regional variants map to the supported neutral language.
        var regional = await _client.SendAsync(Authed(HttpMethod.Put, "/api/v1/account/language", turkish.Token, new { language = "TR-tr" }));
        Assert.Equal("tr", (await JsonAsync(regional)).GetProperty("language").GetString());

        var unsupported = await _client.SendAsync(Authed(HttpMethod.Put, "/api/v1/account/language", turkish.Token, new { language = "de" }));
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Equal("The language 'de' is not supported. Supported languages: en, tr.",
            (await JsonAsync(unsupported)).GetProperty("title").GetString());
        Assert.Equal("tr", (await MeAsync(turkish)).GetProperty("language").GetString());

        var empty = await _client.SendAsync(Authed(HttpMethod.Put, "/api/v1/account/language", turkish.Token, new { language = "" }));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    // Login and refresh store the language only when the client chose one.
    [Fact]
    public async Task LoginAndRefresh_SyncAnExplicitLanguageOnly()
    {
        var username = "loc_" + Guid.NewGuid().ToString("N")[..10];
        await _client.SendAsync(Anonymous(HttpMethod.Post, "/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Password123" }, null));

        var trLogin = await JsonAsync(await _client.SendAsync(Anonymous(HttpMethod.Post, "/api/v1/account/login",
            new { username, password = "Password123" }, "tr")));
        Assert.Equal("tr", trLogin.GetProperty("user").GetProperty("language").GetString());

        // No language in the request: the stored one is kept.
        var plainLogin = await JsonAsync(await _client.SendAsync(Anonymous(HttpMethod.Post, "/api/v1/account/login",
            new { username, password = "Password123" }, null)));
        Assert.Equal("tr", plainLogin.GetProperty("user").GetProperty("language").GetString());

        var refreshed = await JsonAsync(await _client.SendAsync(Anonymous(HttpMethod.Post, "/api/v1/account/refresh",
            new { refreshToken = plainLogin.GetProperty("refreshToken").GetString() }, "en-GB")));
        Assert.Equal("en", refreshed.GetProperty("user").GetProperty("language").GetString());
    }

    // SignalR and push use the RECIPIENT's language; the list uses the REQUEST's.
    [Fact]
    public async Task Notification_RecipientLanguageForPushes_RequestLanguageForList()
    {
        var sender = await RegisterAsync(_client, "loc_");
        var recipient = await RegisterAsync(_client, "loc_", language: "tr");
        var device = "fcm-" + Guid.NewGuid().ToString("N");
        await RegisterDeviceAsync(_client, recipient, device);

        var res = await _client.SendAsync(Authed(HttpMethod.Post, "/api/v1/messages", sender.Token,
            new { recipientId = recipient.UserId, content = "hello" }));
        res.EnsureSuccessStatusCode();
        var senderName = (await MeAsync(sender)).GetProperty("userName").GetString();

        var live = Assert.Single(_factory.Hub.Sent, p => p.UserId == recipient.UserId && p.Method == "ReceiveNotification");
        using (var doc = JsonDocument.Parse(live.Json))
        {
            Assert.Equal($"{senderName} size bir mesaj gönderdi.", doc.RootElement.GetProperty("message").GetString());
        }

        await Eventually(() => _factory.Push.Sent.Any(s => s.DeviceToken == device), "push to the recipient");
        Assert.Equal($"{senderName} size bir mesaj gönderdi.", _factory.Push.Sent.Single(s => s.DeviceToken == device).Message.Body);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.Notifications.SingleAsync(n => n.UserId == recipient.UserId);
            Assert.Equal("notifications.newMessage", stored.MessageKey);
            Assert.Equal($"{senderName} size bir mesaj gönderdi.", stored.Message);
        }

        async Task<string?> ListedAsync(string? language) =>
            (await JsonAsync(await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/notification", recipient.Token, language: language))))[0]
                .GetProperty("message").GetString();
        Assert.Equal($"{senderName} sent you a message.", await ListedAsync(null));
        Assert.Equal($"{senderName} sent you a message.", await ListedAsync("en"));
        Assert.Equal($"{senderName} size bir mesaj gönderdi.", await ListedAsync("tr"));
    }

    // Notifications created before localization have no key and keep their text.
    [Fact]
    public async Task LegacyNotificationWithoutKey_KeepsStoredText()
    {
        var me = await RegisterAsync(_client, "loc_");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Notifications.Add(new Notification { UserId = me.UserId, Message = "Legacy stored text" });
            await db.SaveChangesAsync();
        }

        var list = await JsonAsync(await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/notification", me.Token, language: "tr")));
        Assert.Equal("Legacy stored text", Assert.Single(list.EnumerateArray()).GetProperty("message").GetString());
    }

    // Arguments are stored as JSON and numbers are re-formatted per culture.
    [Fact]
    public void MessageArgs_RoundTripKeepsNumbers()
    {
        var json = MessageArgs.Serialize(new object?[] { "alice", 92.4, 5 });
        var args = MessageArgs.Deserialize(json);
        Assert.Equal("Your taste twin this week: alice (92% compatible, 5 movies in common).",
            LocaleCatalog.Shared.Translate("en", "notifications.tasteTwin", args));
        Assert.Equal("Bu haftanın zevk ikizi: alice (%92 uyumlu, 5 ortak film).",
            LocaleCatalog.Shared.Translate("tr", "notifications.tasteTwin", args));
        Assert.Empty(MessageArgs.Deserialize(null));
    }
}
