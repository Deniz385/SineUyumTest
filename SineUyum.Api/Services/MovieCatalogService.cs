using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using System.Text.Json;

namespace SineUyum.Api.Services
{
    // Movies tablosu, TMDb meta verisinin (başlık, poster) yerel bir
    // önbelleğidir ve tüm kullanıcılar arasında paylaşılır. Önceden istemci bu
    // satırları doğrudan oluşturabiliyordu (over-posting): gerçek bir TMDb
    // id'si ile sahte başlık göndererek diğer kullanıcıların gördüğü veriyi
    // bozabilirdi. Artık bir filmin yerel kaydı YALNIZCA burada, başlık/poster
    // TMDb'den çekilerek oluşturulur; istemci yalnızca movieId verir.
    public class MovieCatalogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly string? _tmdbApiKey;
        private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3/";

        public MovieCatalogService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _tmdbApiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                _tmdbApiKey = configuration["TMDB_API_KEY"];
            }
        }

        // Film yerelde yoksa TMDb'den çekip ekler; her durumda kaydı döndürür.
        // Başlık/poster güvenilir kaynaktan (TMDb) gelir, istemciden değil.
        public async Task<Movie> EnsureMovieAsync(int movieId)
        {
            var existing = await _context.Movies.FindAsync(movieId);
            if (existing != null) return existing;

            var (title, posterPath) = await FetchFromTmdbAsync(movieId);
            var movie = new Movie { Id = movieId, Title = title, PosterPath = posterPath };

            _context.Movies.Add(movie);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Eşzamanlı başka bir istek aynı filmi eklemiş olabilir; onu getir.
                _context.Entry(movie).State = EntityState.Detached;
                var raced = await _context.Movies.FindAsync(movieId);
                if (raced != null) return raced;
                throw;
            }

            return movie;
        }

        private async Task<(string Title, string? PosterPath)> FetchFromTmdbAsync(int movieId)
        {
            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                throw new DomainException(
                    "Film bilgisi alınamıyor: TMDb servisi yapılandırılmamış.",
                    StatusCodes.Status503ServiceUnavailable);
            }

            // Film detayı 24 saat önbelleklenir (MoviesController ile aynı anahtar).
            if (!_cache.TryGetValue($"movie:{movieId}", out string? json) || json == null)
            {
                var client = _httpClientFactory.CreateClient("TMDb");
                var url = $"{TmdbApiBaseUrl}movie/{movieId}?api_key={_tmdbApiKey}&language=tr-TR";
                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(url);
                }
                catch (HttpRequestException)
                {
                    throw new DomainException(
                        "Film bilgisi alınamıyor: TMDb servisine ulaşılamadı.",
                        StatusCodes.Status503ServiceUnavailable);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw DomainException.NotFound($"TMDb'de {movieId} numaralı film bulunamadı.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new DomainException(
                        "Film bilgisi alınamadı.",
                        StatusCodes.Status502BadGateway);
                }

                json = await response.Content.ReadAsStringAsync();
                _cache.Set($"movie:{movieId}", json, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                    Size = 1,
                });
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var posterPath = root.TryGetProperty("poster_path", out var p) ? p.GetString() : null;

            return (title ?? "Başlıksız", posterPath);
        }
    }
}
