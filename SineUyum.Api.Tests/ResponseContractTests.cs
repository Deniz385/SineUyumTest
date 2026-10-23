using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SineUyum.Api.Tests;

// M3: pins down the API contract. A realistic scenario (register, rate, follow,
// lists, messages, notifications, event matching...) goes through every JSON
// endpoint; the SHAPE of every response (status code + field names + value types)
// is compared with the golden file (Contracts/response-shapes.txt). If a field the
// web or mobile client reads is removed/renamed by accident, CI breaks.
//
// To regenerate the golden file after an intentional contract change:
//   UPDATE_CONTRACTS=1 dotnet test --filter ResponseContractTests
// then review the diff and commit it.
//
// Endpoints that pass TMDb through as is (movies/*, compatibility/.../recommendations)
// carry TMDb's schema, not our contract, so they are out of scope.
public class ResponseContractTests : IClassFixture<ResponseContractTests.Factory>
{
    private readonly Factory _factory;

    public ResponseContractTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private sealed class FakeTmdb : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            string body;
            if (path.EndsWith("/movie/popular"))
            {
                body = "{\"results\":[" +
                       "{\"id\":900,\"title\":\"Populer 1\",\"poster_path\":\"/p1.jpg\"}," +
                       "{\"id\":901,\"title\":\"Populer 2\",\"poster_path\":\"/p2.jpg\"}]}";
            }
            else
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                body = $"{{\"id\":{id},\"title\":\"Movie {id}\",\"poster_path\":\"/f{id}.jpg\"}}";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    public class Factory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["TMDb:ApiKey"] = "test-tmdb-key" }));
            builder.ConfigureServices(services =>
                services.AddHttpClient("TMDb").ConfigurePrimaryHttpMessageHandler(() => new FakeTmdb()));
        }
    }

    // --- Shape extraction -------------------------------------------------

    private static string Kind(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "array",
        _ => "object",
    };

    private static void Walk(JsonElement e, string path, SortedSet<string> lines)
    {
        lines.Add($"{(path == "" ? "$" : path)}: {Kind(e)}");
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
                Walk(p.Value, path == "" ? p.Name : $"{path}.{p.Name}", lines);
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
                Walk(item, $"{path}[]", lines);
        }
    }

    private readonly StringBuilder _report = new();
    private readonly HttpClient _client;

    private async Task WaitForMatchingAsync(int eventId, string token)
    {
        for (var i = 0; i < 100; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/events/{eventId}/matching");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var doc = JsonDocument.Parse(await (await _client.SendAsync(req)).Content.ReadAsStringAsync());
            var status = doc.RootElement.GetProperty("status").GetString();
            if (status == "Succeeded") return;
            Assert.NotEqual("Failed", status);
            await Task.Delay(100);
        }
        Assert.Fail("Matching did not finish in 10 seconds.");
    }

    private async Task<JsonElement> Call(string name, HttpMethod method, string url, string? token = null, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // An HttpContent (e.g. multipart) is sent as is, anything else as JSON.
        if (body is HttpContent content) req.Content = content;
        else if (body != null) req.Content = JsonContent.Create(body);

        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();

        // Ids change on every run; mask them to keep the template stable.
        var route = System.Text.RegularExpressions.Regex.Replace(url.Split('?')[0],
            @"/([0-9a-f]{8}-[0-9a-f-]{27}|\d+)(?=/|$)", "/{id}");
        _report.AppendLine($"## {name}  {method} {route} -> {(int)res.StatusCode}");
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(text) &&
            res.Content.Headers.ContentType?.MediaType?.Contains("json") == true)
        {
            root = JsonDocument.Parse(text).RootElement.Clone();
            var lines = new SortedSet<string>(StringComparer.Ordinal);
            Walk(root, "", lines);
            foreach (var l in lines) _report.AppendLine("  " + l);
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            _report.AppendLine("  $: text");
        }
        _report.AppendLine();

        Assert.True(res.IsSuccessStatusCode, $"{name}: unexpected status {(int)res.StatusCode}: {text}");
        return root;
    }

    private Task<JsonElement> Get(string name, string url, string token) => Call(name, HttpMethod.Get, url, token);
    private Task<JsonElement> Post(string name, string url, string? token, object? body = null) => Call(name, HttpMethod.Post, url, token, body);

    private const string GoldenFile = "response-shapes.txt";

    // The comparison reads the file the csproj copies to the output (unaffected by
    // build path mapping). Regeneration writes to the source tree.
    private static string GoldenOutputPath() =>
        Path.Combine(AppContext.BaseDirectory, "Contracts", GoldenFile);

    private static string GoldenSourcePath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "Contracts", GoldenFile);

    // --- Scenario -------------------------------------------------------------

    [Fact]
    public async Task AllEndpointResponseShapes_MatchGoldenFile()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string aName = "ca" + suffix, bName = "cb" + suffix;

        await Call("app.config", HttpMethod.Get, "/api/v1/app/config");

        await Post("account.register", "/api/v1/account/register", null,
            new { username = aName, email = $"{aName}@test.com", password = "Password123" });
        await Post("account.register(b)", "/api/v1/account/register", null,
            new { username = bName, email = $"{bName}@test.com", password = "Password123" });

        var aLogin = await Post("account.login", "/api/v1/account/login", null, new { username = aName, password = "Password123" });
        var bLogin = await Post("account.login(b)", "/api/v1/account/login", null, new { username = bName, password = "Password123" });
        var a = aLogin.GetProperty("token").GetString()!;
        var b = bLogin.GetProperty("token").GetString()!;
        var aId = aLogin.GetProperty("user").GetProperty("id").GetString()!;
        var bId = bLogin.GetProperty("user").GetProperty("id").GetString()!;

        var refreshed = await Post("account.refresh", "/api/v1/account/refresh", null,
            new { refreshToken = aLogin.GetProperty("refreshToken").GetString() });
        a = refreshed.GetProperty("token").GetString()!;
        await Get("account.me", "/api/v1/account/me", a);
        await Call("account.language", HttpMethod.Put, "/api/v1/account/language", a, new { language = "en" });
        await Post("devices.register", "/api/v1/devices", a, new { token = "fcm-contract-" + suffix, platform = "android" });

        // Rating (the movie is fetched from TMDb) and a common movie.
        await Post("ratings.add", "/api/v1/ratings", a, new { movieId = 550, rating = 9 });
        await Post("ratings.add(b)", "/api/v1/ratings", b, new { movieId = 550, rating = 9 });
        await Post("ratings.addmovie", "/api/v1/ratings/addmovie", a, new { id = 551 });

        await Get("account.search", $"/api/v1/account/search?query=cb{suffix}", a);
        await Get("compatibility.get", $"/api/v1/compatibility/{bId}", a);

        // Following.
        await Post("follow.follow", $"/api/v1/follow/{bId}", a);
        await Get("follow.status", $"/api/v1/follow/{bId}/status", a);
        await Get("follow.followers", $"/api/v1/follow/{bId}/followers", a);
        await Get("follow.following", $"/api/v1/follow/{aId}/following", a);

        // Social core: reviews, likes, comments, feed.
        var review = await Call("reviews.upsert", HttpMethod.Put, "/api/v1/reviews/movie/550", b, new { text = "Watch it", containsSpoilers = false });
        var reviewId = review.GetProperty("id").GetInt32();
        await Get("reviews.forMovie", "/api/v1/reviews/movie/550", a);
        await Post("reviews.like", $"/api/v1/reviews/{reviewId}/like", a);
        var comment = await Post("reviews.comment", $"/api/v1/reviews/{reviewId}/comments", a, new { text = "I agree" });
        await Get("reviews.comments", $"/api/v1/reviews/{reviewId}/comments", a);
        await Get("feed", "/api/v1/feed", a);
        await Get("tasteTwin(none)", "/api/v1/taste-twin", a);
        await Call("reviews.deleteComment", HttpMethod.Delete, $"/api/v1/reviews/comments/{comment.GetProperty("id").GetInt32()}", a);
        await Call("reviews.unlike", HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/like", a);

        // Film diary: an entry with the (unchanged) rating, one for a movie fetched
        // from TMDb without a rating (rating: null), edit, list, summary, delete.
        var diaryEntry = await Post("diary.create", "/api/v1/diary", a,
            new { movieId = 550, watchedOn = "2025-03-10", rewatch = false, note = "First time", rating = 9 });
        var diaryRewatch = await Post("diary.create(noRating)", "/api/v1/diary", a,
            new { movieId = 553, watchedOn = "2025-04-01", rewatch = true });
        await Call("diary.update", HttpMethod.Put, $"/api/v1/diary/{diaryEntry.GetProperty("id").GetInt32()}", a,
            new { watchedOn = "2025-03-11", rewatch = false, note = "Edited" });
        await Get("diary.list", $"/api/v1/diary/user/{aId}?year=2025", b);
        await Get("diary.summary", $"/api/v1/diary/user/{aId}/summary?year=2025", b);
        await Get("diary.forMovie", $"/api/v1/diary/movie/{diaryEntry.GetProperty("movie").GetProperty("id").GetInt32()}", a);
        await Get("yearInReview", $"/api/v1/year-in-review/user/{aId}?year=2025", b);
        await Call("diary.delete", HttpMethod.Delete, $"/api/v1/diary/{diaryRewatch.GetProperty("id").GetInt32()}", a);

        // Watchlist.
        var list = await Post("watchlist.create", "/api/v1/watchlist", a, new { name = "List", description = "Description", visibility = "Public" });
        var listId = list.GetProperty("id").GetInt32();
        await Get("watchlist.list", "/api/v1/watchlist", a);
        await Post("watchlist.addMovie", $"/api/v1/watchlist/{listId}/movies", a, new { movieId = 552, title = "Client title", posterPath = "/client.jpg" });
        await Get("watchlist.details", $"/api/v1/watchlist/{listId}", a);
        await Get("watchlist.public", $"/api/v1/watchlist/public/{listId}", b);
        await Call("watchlist.update", HttpMethod.Put, $"/api/v1/watchlist/{listId}", a, new { name = "List 2", description = "x" });
        await Post("watchlist.like", $"/api/v1/watchlist/{listId}/like", b, null);
        await Post("watchlist.save", $"/api/v1/watchlist/{listId}/save", b, null);
        await Get("watchlist.discover", "/api/v1/watchlist/discover?sort=trending", b);
        await Get("watchlist.userLists", $"/api/v1/watchlist/user/{aId}", b);
        await Get("watchlist.saved", "/api/v1/watchlist/saved", b);
        await Call("watchlist.unsave", HttpMethod.Delete, $"/api/v1/watchlist/{listId}/save", b);
        await Call("watchlist.unlike", HttpMethod.Delete, $"/api/v1/watchlist/{listId}/like", b);

        // Messages: plain text, a movie suggestion, a shared list.
        await Post("messages.create", "/api/v1/messages", a, new { recipientId = bId, content = "hi" });
        await Post("messages.create(movie)", "/api/v1/messages", a, new { recipientId = bId, movieId = 550 });
        await Post("messages.create(list)", "/api/v1/messages", a, new { recipientId = bId, watchlistId = listId });
        await Get("messages.thread", $"/api/v1/messages/thread/{aId}", b);
        await Get("messages.conversations", "/api/v1/messages", b);
        await Post("messages.markThreadRead", $"/api/v1/messages/thread/{aId}/mark-as-read", b);

        // Notifications.
        var notifications = await Get("notification.list", "/api/v1/notification", b);
        var notificationId = notifications[0].GetProperty("id").GetInt32();
        await Post("notification.markOne", $"/api/v1/notification/{notificationId}/mark-as-read", b);
        await Post("notification.markAll", "/api/v1/notification/mark-all-as-read", b);

        // Profile.
        await Get("profile.get", $"/api/v1/profile/{aId}", b);
        await Get("profile.identity", $"/api/v1/profile/{aId}/identity", b);
        await Call("profile.update", HttpMethod.Put, "/api/v1/profile", a, new { bio = "bio", profileImageUrl = (string?)null });

        // Profile photo: upload (later responses show profileImageUrl filled),
        // remove it once and upload again.
        MultipartFormDataContent Photo()
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(TestImages.Jpeg(300, 300)), "file", "photo.jpg");
            return form;
        }
        await Post("profile.photo.upload", "/api/v1/profile/photo", a, Photo());
        await Call("profile.photo.delete", HttpMethod.Delete, "/api/v1/profile/photo", a);
        await Post("profile.photo.upload(2)", "/api/v1/profile/photo", a, Photo());

        // Event lifecycle: NONE -> AVAILABLE -> PENDING -> MATCHED.
        var adminLogin = await Post("account.login(admin)", "/api/v1/account/login", null,
            new { username = TestWebAppFactory.AdminUserName, password = TestWebAppFactory.AdminPassword });
        var admin = adminLogin.GetProperty("token").GetString()!;

        // The class uses its own factory (and database), so there are no events.
        await Get("event.myStatus(NONE)", "/api/v1/event/my-status", a);
        var ev = await Post("event.create", "/api/v1/event", admin,
            new { eventDate = DateTime.UtcNow.AddDays(7), locationName = "Cinema", address = "Address", groupSize = 2 });
        var eventId = ev.GetProperty("id").GetInt32();
        await Get("event.list", "/api/v1/event", admin);
        await Get("event.myStatus(AVAILABLE)", "/api/v1/event/my-status", a);
        // Events are a paid feature (G4): a subscription is needed before joining.
        await Get("subscription.status(none)", "/api/v1/subscription", a);
        await Post("admin.grantSubscription", $"/api/v1/admin/users/{aId}/subscription", admin, new { days = 30 });
        await Post("admin.grantSubscription(b)", $"/api/v1/admin/users/{bId}/subscription", admin, new { days = 30 });
        await Get("subscription.status(active)", "/api/v1/subscription", a);
        await Post("event.join", $"/api/v1/event/{eventId}/join", a);
        await Post("event.join(b)", $"/api/v1/event/{eventId}/join", b);
        await Get("event.myStatus(PENDING)", "/api/v1/event/my-status", a);
        await Post("admin.createGroups", $"/api/v1/admin/events/{eventId}/create-groups", admin);
        // Matching runs in the background (M7): poll until done, record the final shape.
        await WaitForMatchingAsync(eventId, admin);
        await Get("admin.matching(succeeded)", $"/api/v1/admin/events/{eventId}/matching", admin);
        var matched = await Get("event.myStatus(MATCHED)", "/api/v1/event/my-status", a);
        var groupId = matched.GetProperty("group").GetProperty("id").GetInt32();
        var movieToVote = matched.GetProperty("suggestedMovies")[0].GetProperty("id").GetInt32();
        await Post("event.vote", "/api/v1/event/vote", a, new { groupId, movieId = movieToVote });
        await Get("event.myStatus(MATCHED,voted)", "/api/v1/event/my-status", a);
        await Call("event.update", HttpMethod.Put, $"/api/v1/event/{eventId}", admin,
            new { eventDate = DateTime.UtcNow.AddDays(8), locationName = "Cinema 2", address = "Address", groupSize = 2 });

        var ev2 = await Post("event.create(2)", "/api/v1/event", admin,
            new { eventDate = DateTime.UtcNow.AddDays(30), locationName = "To be deleted", address = (string?)null, groupSize = 4 });
        await Call("event.delete", HttpMethod.Delete, $"/api/v1/event/{ev2.GetProperty("id").GetInt32()}", admin);

        // Cleanup endpoints.
        await Call("watchlist.removeMovie", HttpMethod.Delete, $"/api/v1/watchlist/{listId}/movies/552", a);
        await Call("watchlist.delete", HttpMethod.Delete, $"/api/v1/watchlist/{listId}", a);
        await Call("follow.unfollow", HttpMethod.Delete, $"/api/v1/follow/{bId}", a);

        // Privacy (G12): settings, the restricted shape of a private profile, blocking.
        await Get("account.privacy.get", "/api/v1/account/privacy", b);
        await Call("account.privacy.update", HttpMethod.Put, "/api/v1/account/privacy", b,
            new { profileVisibility = "Private", messagePermission = "Following", hiddenFromSearch = true });
        await Get("profile.get(restricted)", $"/api/v1/profile/{bId}", a);
        await Post("blocks.block", $"/api/v1/blocks/{bId}", a);
        await Get("blocks.list", "/api/v1/blocks", a);
        await Call("blocks.unblock", HttpMethod.Delete, $"/api/v1/blocks/{bId}", a);
        var changed = await Post("account.changePassword", "/api/v1/account/change-password", a,
            new { currentPassword = "Password123", newPassword = "YeniPassword123" });
        await Post("devices.unregister", "/api/v1/devices/unregister", null, new { token = "fcm-contract-" + suffix });
        await Post("account.logout", "/api/v1/account/logout", changed.GetProperty("token").GetString()!,
            new { refreshToken = changed.GetProperty("refreshToken").GetString() });

        var actual = _report.ToString().Replace("\r\n", "\n");

        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACTS") == "1")
        {
            var source = GoldenSourcePath();
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, actual);
            return;
        }

        var golden = GoldenOutputPath();
        Assert.True(File.Exists(golden), $"Golden file missing: {golden}. Generate it with UPDATE_CONTRACTS=1.");
        var expected = File.ReadAllText(golden).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }
}
