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

            var details = await FetchFromTmdbAsync(movieId);

            // İki deneme: eşzamanlı başka bir istek aynı filmi ya da aynı yeni
            // türü eklemiş olabilir; ikinci denemede tür artık veritabanındadır.
            for (var attempt = 1; ; attempt++)
            {
                var movie = new Movie { Id = movieId, Title = details.Title, PosterPath = details.PosterPath, TmdbVoteAverage = details.VoteAverage };
                await AttachGenresAsync(movie, details.Genres);
                _context.Movies.Add(movie);
                try
                {
                    await _context.SaveChangesAsync();
                    return movie;
                }
                catch (DbUpdateException) when (attempt < 2)
                {
                    // Yalnızca bu denemenin eklediklerini bırak; çağıranın bağlamdaki
                    // diğer bekleyen değişiklikleri korunur.
                    var genreIds = details.Genres.Select(g => g.Id).ToHashSet();
                    foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                    {
                        if (entry.Entity is Movie { Id: var id } && id == movieId ||
                            entry.Entity is MovieGenre { MovieId: var mid } && mid == movieId ||
                            entry.Entity is Genre { Id: var gid } && genreIds.Contains(gid))
                        {
                            entry.State = EntityState.Detached;
                        }
                    }
                    var raced = await _context.Movies.FindAsync(movieId);
                    if (raced != null) return raced;
                }
            }
        }

        // Türleri eksik bir filmin türlerini TMDb'den tamamlar (GenreBackfillWorker).
        // TMDb'de türü olmayan film de işlenmiş sayılır; tekrar sorulmaz.
        public async Task SyncGenresAsync(int movieId)
        {
            var movie = await _context.Movies.Include(m => m.MovieGenres).FirstOrDefaultAsync(m => m.Id == movieId);
            if (movie == null) return;

            var details = await FetchFromTmdbAsync(movieId);
            if (movie.MovieGenres.Count == 0) await AttachGenresAsync(movie, details.Genres);
            movie.GenresSyncedAt = DateTime.UtcNow;
            movie.TmdbVoteAverage = details.VoteAverage;
            await _context.SaveChangesAsync();
        }

        private async Task AttachGenresAsync(Movie movie, IReadOnlyList<(int Id, string Name)> genres)
        {
            foreach (var (id, name) in genres.DistinctBy(g => g.Id))
            {
                var genre = await _context.Genres.FindAsync(id);
                if (genre == null)
                {
                    genre = new Genre { Id = id, Name = name };
                    _context.Genres.Add(genre);
                }
                movie.MovieGenres.Add(new MovieGenre { Movie = movie, Genre = genre });
            }
            movie.GenresSyncedAt = DateTime.UtcNow;
        }

        private async Task<(string Title, string? PosterPath, IReadOnlyList<(int Id, string Name)> Genres, double? VoteAverage)> FetchFromTmdbAsync(int movieId)
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

            var genres = new List<(int, string)>();
            if (root.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in g.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var gid) && gid.TryGetInt32(out var genreId) &&
                        item.TryGetProperty("name", out var gname) && gname.GetString() is { Length: > 0 } genreName)
                    {
                        genres.Add((genreId, genreName));
                    }
                }
            }

            // Oy yoksa TMDb 0 döner; "ortalama yok" sayılır.
            double? voteAverage = root.TryGetProperty("vote_average", out var va) && va.TryGetDouble(out var avg) && avg > 0
                ? Math.Round(avg, 1)
                : null;

            return (title ?? "Başlıksız", posterPath, genres, voteAverage);
        }
    }
}
