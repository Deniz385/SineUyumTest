using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SineUyum.Api.Tests;

// G7: /api/v1/ratings/addmovie ve /api/v1/ratings istemciden gelen başlık/poster'a
// güvenmemeli; meta veri TMDb'den gelmeli. Sahte handler gerçek TMDb başlığını
// döndürür; istemci kasten yanlış başlık gönderir.
public class MovieOverPostingTests : IClassFixture<MovieOverPostingTests.TmdbStubFactory>
{
    private readonly TmdbStubFactory _factory;

    public MovieOverPostingTests(TmdbStubFactory factory) => _factory = factory;

    public sealed class MovieStubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // TMDb'nin döndürdüğü gerçek başlık.
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":550,\"title\":\"TMDb Gerçek Başlık\",\"poster_path\":\"/gercek.jpg\"}",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    public class TmdbStubFactory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TMDb:ApiKey"] = "test-tmdb-key",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("TMDb")
                    .ConfigurePrimaryHttpMessageHandler(() => new MovieStubHandler());
            });
        }
    }

    private async Task<string> UserTokenAsync(HttpClient client)
    {
        var username = "op_" + Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/v1/account/register",
            new { username, email = $"{username}@test.com", password = "Sifre123" });
        var login = await client.PostAsJsonAsync("/api/v1/account/login",
            new { username, password = "Sifre123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task AddMovie_IstemciBasligiYokSayilir_TmdbBasligiKaydedilir()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        // İstemci kasten yanlış başlık gönderiyor.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ratings/addmovie")
        {
            Content = JsonContent.Create(new
            {
                id = 550,
                title = "SAHTE ZEHIRLI BAŞLIK",
                posterPath = "/sahte.jpg",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var title = doc.RootElement.GetProperty("title").GetString();

        // İstemcinin gönderdiği başlık DEĞİL, TMDb'den gelen başlık kaydedilmeli.
        Assert.Equal("TMDb Gerçek Başlık", title);
        Assert.NotEqual("SAHTE ZEHIRLI BAŞLIK", title);
    }

    [Fact]
    public async Task Rate_FilmYerelDeYoksa_TmdbdenCekilipEklenir()
    {
        var client = _factory.CreateClient();
        var token = await UserTokenAsync(client);

        // addmovie çağrısı olmadan doğrudan puan ver — film TMDb'den çekilmeli.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ratings")
        {
            Content = JsonContent.Create(new { movieId = 550, rating = 9 }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
