using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// M7: matching runs in a background job, not in the request path, in a single transaction.
public class MatchingJobTests : IClassFixture<MatchingJobTests.Factory>
{
    // A TMDb "popular movies" fake; returns 500 from FailFromCall on.
    public sealed class FakeTmdb : DelegatingHandler
    {
        private int _calls;
        public volatile int FailFromCall = int.MaxValue;
        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call >= FailFromCall)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
            const string body = "{\"results\":[" +
                "{\"id\":940001,\"title\":\"Popular 1\",\"poster_path\":\"/p1.jpg\"}," +
                "{\"id\":940002,\"title\":\"Popular 2\",\"poster_path\":\"/p2.jpg\"}," +
                "{\"id\":940003,\"title\":\"Popular 3\",\"poster_path\":\"/p3.jpg\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    public sealed class Factory : TestWebAppFactory
    {
        public FakeTmdb Tmdb { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TMDb:ApiKey"] = "test-tmdb-key",
                ["Matching:PollSeconds"] = "1",
                ["Matching:StaleRunMinutes"] = "5",
            }));
            builder.ConfigureServices(services =>
                services.AddHttpClient("TMDb").ConfigurePrimaryHttpMessageHandler(() => Tmdb));
        }
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;

    public MatchingJobTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> AdminTokenAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/account/login",
            new { username = TestWebAppFactory.AdminUserName, password = TestWebAppFactory.AdminPassword });
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, string? language = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (language != null) req.Headers.AcceptLanguage.ParseAdd(language);
        return await _client.SendAsync(req);
    }

    // Participants without ratings: every group's suggestion comes from TMDb.
    private async Task<(int EventId, string Location, List<string> UserIds)> SeedEventAsync(int participants, int groupSize = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var location = "Hall " + Guid.NewGuid().ToString("N")[..8];
        var ev = new CinemaEvent { EventDate = DateTime.UtcNow.AddDays(4), LocationName = location, GroupSize = groupSize };
        db.CinemaEvents.Add(ev);
        var userIds = new List<string>();
        for (var i = 0; i < participants; i++)
        {
            var user = new AppUser { UserName = "mj_" + Guid.NewGuid().ToString("N")[..10] };
            db.Users.Add(user);
            userIds.Add(user.Id);
        }
        await db.SaveChangesAsync();
        foreach (var id in userIds) db.EventParticipants.Add(new EventParticipant { CinemaEventId = ev.Id, UserId = id });
        await db.SaveChangesAsync();
        return (ev.Id, location, userIds);
    }

    private async Task<JsonElement> WaitForFinishAsync(int eventId, string token)
    {
        for (var i = 0; i < 150; i++)
        {
            var res = await SendAsync(HttpMethod.Get, $"/api/v1/admin/events/{eventId}/matching", token);
            if (res.StatusCode == HttpStatusCode.OK)
            {
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                var status = doc.RootElement.GetProperty("status").GetString();
                if (status is "Succeeded" or "Failed") return doc.RootElement.Clone();
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("The matching job did not finish in 15 seconds.");
    }

    private async Task<(int Groups, int Notifications)> CountAsync(int eventId, string location)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (
            await db.EventGroups.CountAsync(g => g.CinemaEventId == eventId),
            await db.Notifications.CountAsync(n => n.Message.Contains(location)));
    }

    [Fact]
    public async Task Request_AcceptedImmediately_JobCompletesInBackground()
    {
        var admin = await AdminTokenAsync();
        var (eventId, location, users) = await SeedEventAsync(participants: 3);

        var res = await SendAsync(HttpMethod.Post, $"/api/v1/admin/events/{eventId}/create-groups", admin);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        Assert.EndsWith($"/api/v1/admin/events/{eventId}/matching", res.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
        using (var accepted = JsonDocument.Parse(await res.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Queued", accepted.RootElement.GetProperty("status").GetString());
        }

        var done = await WaitForFinishAsync(eventId, admin);
        Assert.Equal("Succeeded", done.GetProperty("status").GetString());
        Assert.Equal(1, done.GetProperty("groupCount").GetInt32());
        Assert.Equal(1, done.GetProperty("ungroupedCount").GetInt32());
        Assert.Equal("Groups created: 1; participants left without a group: 1.", done.GetProperty("message").GetString());

        // The same run read in Turkish.
        using (var tr = JsonDocument.Parse(await (await SendAsync(HttpMethod.Get, $"/api/v1/admin/events/{eventId}/matching", admin, "tr")).Content.ReadAsStringAsync()))
        {
            Assert.Equal("1 grup oluşturuldu; 1 kişi gruba alınamadı.", tr.RootElement.GetProperty("message").GetString());
        }

        var (groups, notifications) = await CountAsync(eventId, location);
        Assert.Equal(1, groups);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public async Task FailureMidway_LeavesNoGroups_RetryWorks()
    {
        var admin = await AdminTokenAsync();
        var (eventId, location, _) = await SeedEventAsync(participants: 4);

        // The first group's suggestion arrives, TMDb fails for the second. The
        // old code kept the first group and its notifications, and rejected a retry.
        _factory.Tmdb.FailFromCall = _factory.Tmdb.Calls + 2;
        try
        {
            Assert.Equal(HttpStatusCode.Accepted,
                (await SendAsync(HttpMethod.Post, $"/api/v1/admin/events/{eventId}/create-groups", admin)).StatusCode);
            var failed = await WaitForFinishAsync(eventId, admin);

            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal(LocaleCatalog.Shared.Translate("en", MatchingWorker.UnexpectedErrorKey), failed.GetProperty("error").GetString());
            Assert.DoesNotContain("Exception", failed.GetRawText());
            Assert.Equal((0, 0), await CountAsync(eventId, location));
        }
        finally
        {
            _factory.Tmdb.FailFromCall = int.MaxValue;
        }

        Assert.Equal(HttpStatusCode.Accepted,
            (await SendAsync(HttpMethod.Post, $"/api/v1/admin/events/{eventId}/create-groups", admin)).StatusCode);
        var retried = await WaitForFinishAsync(eventId, admin);
        Assert.Equal("Succeeded", retried.GetProperty("status").GetString());
        Assert.Equal((2, 4), await CountAsync(eventId, location));
    }

    [Fact]
    public async Task PreCheckErrors_ReturnImmediately_NoJobCreated()
    {
        var admin = await AdminTokenAsync();
        var (eventId, _, _) = await SeedEventAsync(participants: 1);

        var res = await SendAsync(HttpMethod.Post, $"/api/v1/admin/events/{eventId}/create-groups", admin);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using (var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync()))
        {
            Assert.StartsWith("Groups could not be created because there are not enough participants",
                problem.RootElement.GetProperty("title").GetString());
        }

        var trRes = await SendAsync(HttpMethod.Post, $"/api/v1/admin/events/{eventId}/create-groups", admin, "tr");
        using (var trProblem = JsonDocument.Parse(await trRes.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Yeterli katılımcı olmadığı için gruplar oluşturulamadı. Gerekli: 2, Mevcut: 1",
                trProblem.RootElement.GetProperty("title").GetString());
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, $"/api/v1/admin/events/{eventId}/matching", admin)).StatusCode);
    }

    [Fact]
    public async Task WhileAJobRuns_SecondRequest409()
    {
        var admin = await AdminTokenAsync();
        var (eventId, _, _) = await SeedEventAsync(participants: 2);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // A job another server instance just took.
            db.MatchingRuns.Add(new MatchingRun { CinemaEventId = eventId, Status = MatchingRunStatus.Running, StartedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var res = await SendAsync(HttpMethod.Post, $"/api/v1/admin/events/{eventId}/create-groups", admin);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task AbandonedJob_IsPickedUpAgainAndCompletes()
    {
        var admin = await AdminTokenAsync();
        var (eventId, location, _) = await SeedEventAsync(participants: 2);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // The process that took it crashed an hour ago.
            db.MatchingRuns.Add(new MatchingRun
            {
                CinemaEventId = eventId,
                Status = MatchingRunStatus.Running,
                StartedAt = DateTime.UtcNow.AddHours(-1)
            });
            await db.SaveChangesAsync();
        }

        var done = await WaitForFinishAsync(eventId, admin);
        Assert.Equal("Succeeded", done.GetProperty("status").GetString());
        Assert.Equal((1, 2), await CountAsync(eventId, location));
    }
}
