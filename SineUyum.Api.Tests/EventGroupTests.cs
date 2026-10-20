using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// M8: grup önerileri virgüllü metin yerine EventGroupSuggestions tablosunda.
// Sıra korunur, oy yalnızca önerilen filmlere verilir.
public class EventGroupTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public EventGroupTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Session(string Token, string Id);

    private async Task<Session> LoginAsync(string username, string password)
    {
        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new { username, password });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new Session(
            doc.RootElement.GetProperty("token").GetString()!,
            doc.RootElement.GetProperty("user").GetProperty("id").GetString()!);
    }

    private async Task<Session> RegisterAsync()
    {
        var username = "grp_" + Guid.NewGuid().ToString("N")[..10];
        await _client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Sifre123" });
        return await LoginAsync(username, "Sifre123");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session s, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        if (body != null) req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private async Task GrantSubscriptionAsync(Session user)
    {
        var admin = await LoginAsync(TestWebAppFactory.AdminUserName, TestWebAppFactory.AdminPassword);
        var res = await SendAsync(HttpMethod.Post, $"/api/v1/admin/users/{user.Id}/subscription", admin, new { days = 30 });
        res.EnsureSuccessStatusCode();
    }

    // Grubu doğrudan veritabanına kurar; öneri sırası id sırasından farklı.
    private async Task<int> SeedGroupAsync(IEnumerable<Session> members, params int[] suggestionsInOrder)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var id in suggestionsInOrder)
        {
            if (!await db.Movies.AnyAsync(m => m.Id == id)) db.Movies.Add(new Movie { Id = id, Title = $"Film {id}" });
        }
        var ev = new CinemaEvent { EventDate = DateTime.UtcNow.AddDays(5), LocationName = "Salon", GroupSize = 2 };
        db.CinemaEvents.Add(ev);
        var group = new EventGroup { CinemaEvent = ev };
        foreach (var m in members) group.Members.Add(new EventGroupMember { UserId = m.Id });
        for (var i = 0; i < suggestionsInOrder.Length; i++)
        {
            group.Suggestions.Add(new EventGroupSuggestion { MovieId = suggestionsInOrder[i], Rank = i });
        }
        db.EventGroups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    [Fact]
    public async Task Durum_OnerileriSirasiylaDoner_EskiAlanUyumlu()
    {
        var me = await RegisterAsync();
        await SeedGroupAsync([me], 930003, 930001, 930002);

        var res = await SendAsync(HttpMethod.Get, "/api/v1/event/my-status", me);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("MATCHED", root.GetProperty("status").GetString());
        var ids = root.GetProperty("suggestedMovies").EnumerateArray().Select(m => m.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([930003, 930001, 930002], ids);
        Assert.Equal("930003,930001,930002", root.GetProperty("group").GetProperty("suggestedMovieIds").GetString());
    }

    [Fact]
    public async Task Oy_YalnizcaOnerilenFilmlere()
    {
        var me = await RegisterAsync();
        var other = await RegisterAsync();
        await GrantSubscriptionAsync(me);
        await GrantSubscriptionAsync(other);
        var groupId = await SeedGroupAsync([me], 930011, 930012);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Movies.Add(new Movie { Id = 930019, Title = "Önerilmeyen" });
            await db.SaveChangesAsync();
        }

        var notSuggested = await SendAsync(HttpMethod.Post, "/api/v1/event/vote", me, new { groupId, movieId = 930019 });
        Assert.Equal(HttpStatusCode.BadRequest, notSuggested.StatusCode);
        // Olmayan film önceden FK hatasıyla 500 veriyordu.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Post, "/api/v1/event/vote", me, new { groupId, movieId = 1 })).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Post, "/api/v1/event/vote", me, new { groupId, movieId = 930012 })).StatusCode);

        // Grupta olmayan: önceden Forbid("...") şema sanılıp 500 dönüyordu.
        var outsider = await SendAsync(HttpMethod.Post, "/api/v1/event/vote", other, new { groupId, movieId = 930012 });
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        using var problem = JsonDocument.Parse(await outsider.Content.ReadAsStringAsync());
        Assert.Equal("Bu grupta oy kullanma yetkiniz yok.", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task BaskasininListesi_Paylasilamaz_403()
    {
        var owner = await RegisterAsync();
        var me = await RegisterAsync();
        var list = await SendAsync(HttpMethod.Post, "/api/v1/watchlist", owner, new { name = "Onun listesi" });
        list.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var listId = doc.RootElement.GetProperty("id").GetInt32();

        var res = await SendAsync(HttpMethod.Post, "/api/v1/messages", me, new { recipientId = owner.Id, watchlistId = listId });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Eslestirme_OnerileriTabloyaSirayla_Yazar()
    {
        var a = await RegisterAsync();
        var b = await RegisterAsync();
        var outsider = await RegisterAsync();
        int eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var id in new[] { 930021, 930022, 930024 })
            {
                if (!await db.Movies.AnyAsync(m => m.Id == id)) db.Movies.Add(new Movie { Id = id, Title = $"Film {id}" });
            }
            await db.SaveChangesAsync();
            foreach (var u in new[] { a, b })
            {
                db.UserRatings.Add(new UserRating { UserId = u.Id, MovieId = 930021, Rating = 9 });
            }
            // Grup dışı benzer kullanıcı: grubun sevdiği filmi sever, ayrıca 930022 ve 930024'ü.
            db.UserRatings.Add(new UserRating { UserId = outsider.Id, MovieId = 930021, Rating = 9 });
            db.UserRatings.Add(new UserRating { UserId = outsider.Id, MovieId = 930022, Rating = 10 });
            db.UserRatings.Add(new UserRating { UserId = outsider.Id, MovieId = 930024, Rating = 9 });
            var ev = new CinemaEvent { EventDate = DateTime.UtcNow.AddDays(5), LocationName = "Salon", GroupSize = 2 };
            db.CinemaEvents.Add(ev);
            await db.SaveChangesAsync();
            db.EventParticipants.Add(new EventParticipant { CinemaEventId = ev.Id, UserId = a.Id });
            db.EventParticipants.Add(new EventParticipant { CinemaEventId = ev.Id, UserId = b.Id });
            await db.SaveChangesAsync();
            eventId = ev.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MatchingService>().CreateGroupsForEventAsync(eventId);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var suggestions = await db.EventGroupSuggestions
                .Where(s => s.EventGroup.CinemaEventId == eventId)
                .OrderBy(s => s.Rank)
                .Select(s => new { s.MovieId, s.Rank })
                .ToListAsync();

            Assert.Equal(2, suggestions.Count);
            Assert.Equal([0, 1], suggestions.Select(s => s.Rank));
            Assert.Equal([930022, 930024], suggestions.Select(s => s.MovieId).Order());
        }
    }
}
