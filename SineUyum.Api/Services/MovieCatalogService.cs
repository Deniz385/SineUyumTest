using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using System.Text.Json;

namespace SineUyum.Api.Services
{
    // The Movies table is a local cache of TMDb metadata (title, poster) shared
    // by all users. The client used to be able to create these rows directly
    // (over-posting): sending a real TMDb id with a fake title corrupted what
    // other users saw. Now a movie's local row is created ONLY here, with the
    // title/poster fetched from TMDb; the client only sends a movieId.
    //
    // The catalog stores ONE title per movie, fetched in a fixed language
    // (CatalogLanguage, Turkish as before). Known follow-up: per-language
    // titles for the stored catalog (e.g. a MovieTranslations table, or
    // fetching titles per request language) so English users see English
    // titles in lists, reviews, the feed and identity cards. The TMDb proxy
    // endpoints (MoviesController) already follow the request language.
    public class MovieCatalogService
    {
        // Language of the titles stored in the shared catalog.
        public const string CatalogLanguage = "tr";
        public static readonly string CatalogTmdbLanguage = SupportedLanguages.TmdbLanguage(CatalogLanguage);

        // Fallback title for movies without one, in the catalog's language.
        public static string UntitledTitle => LocaleCatalog.Shared.Translate(CatalogLanguage, "movies.untitled");

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

        // Fetches and adds the movie from TMDb if it is not stored locally;
        // returns the row either way. Title/poster come from the trusted
        // source (TMDb), not from the client.
        public async Task<Movie> EnsureMovieAsync(int movieId)
        {
            var existing = await _context.Movies.FindAsync(movieId);
            if (existing != null) return existing;

            var details = await FetchFromTmdbAsync(movieId);

            // Two attempts: a concurrent request may have added the same movie
            // or the same new genre; on the second attempt the genre is in the database.
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
                    // Detach only what this attempt added; the caller's other
                    // pending changes in the context are kept.
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

        // Fills in the genres of a movie that has none from TMDb (GenreBackfillWorker).
        // A movie without genres on TMDb also counts as processed; it is not asked again.
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
                    "movies.tmdbNotConfigured",
                    StatusCodes.Status503ServiceUnavailable);
            }

            // Movie details are cached for 24 hours (same key as MoviesController in this language).
            var cacheKey = $"movie:{CatalogTmdbLanguage}:{movieId}";
            if (!_cache.TryGetValue(cacheKey, out string? json) || json == null)
            {
                var client = _httpClientFactory.CreateClient("TMDb");
                var url = $"{TmdbApiBaseUrl}movie/{movieId}?api_key={_tmdbApiKey}&language={CatalogTmdbLanguage}";
                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(url);
                }
                catch (HttpRequestException)
                {
                    throw new DomainException(
                        "movies.tmdbUnreachable",
                        StatusCodes.Status503ServiceUnavailable);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw DomainException.NotFound("movies.notFoundOnTmdb", movieId);
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new DomainException(
                        "movies.fetchFailed",
                        StatusCodes.Status502BadGateway);
                }

                json = await response.Content.ReadAsStringAsync();
                _cache.Set(cacheKey, json, new MemoryCacheEntryOptions
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

            // TMDb returns 0 when there are no votes; that counts as "no average".
            double? voteAverage = root.TryGetProperty("vote_average", out var va) && va.TryGetDouble(out var avg) && avg > 0
                ? Math.Round(avg, 1)
                : null;

            return (title ?? UntitledTitle, posterPath, genres, voteAverage);
        }
    }
}
