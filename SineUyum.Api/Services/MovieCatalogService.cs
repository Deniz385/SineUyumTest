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
    // Titles are stored per language (MovieTranslations). Movie.Title is the
    // fallback: the title in the language the movie was first fetched in.
    // A missing translation is added when someone opens or rates the movie in
    // that language (usually a cache hit: same request as the details page),
    // and MovieTranslationWorker fills in the rest in the background.
    public class MovieCatalogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MovieCatalogService> _logger;
        private readonly string? _tmdbApiKey;
        private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3/";

        public MovieCatalogService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<MovieCatalogService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
            _tmdbApiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                _tmdbApiKey = configuration["TMDB_API_KEY"];
            }
        }

        // Fallback title for movies without one.
        public static string UntitledTitle(string language) => LocaleCatalog.Shared.Translate(language, "movies.untitled");

        // The movie details request, shared with MoviesController.GetMovieById:
        // same URL (with credits) and cache key, so a details page viewed in a
        // language also serves the catalog, and the details page never gets a
        // cached copy without credits.
        public static string DetailsCacheKey(string language, int movieId) =>
            $"movie:{SupportedLanguages.TmdbLanguage(language)}:{movieId}";

        public static string DetailsUrl(string apiKey, string language, int movieId) =>
            $"{TmdbApiBaseUrl}movie/{movieId}?api_key={apiKey}&language={SupportedLanguages.TmdbLanguage(language)}&append_to_response=credits";

        // Fetches and adds the movie from TMDb if it is not stored locally, in
        // the request language; returns the row either way. Title/poster come
        // from the trusted source (TMDb), not from the client.
        public Task<Movie> EnsureMovieAsync(int movieId) => EnsureMovieAsync(movieId, SupportedLanguages.Current);

        public async Task<Movie> EnsureMovieAsync(int movieId, string language)
        {
            var existing = await _context.Movies.FindAsync(movieId);
            if (existing != null)
            {
                await TryAddTranslationAsync(existing, language);
                return existing;
            }

            var details = await FetchFromTmdbAsync(movieId, language);

            // Two attempts: a concurrent request may have added the same movie
            // or the same new genre; on the second attempt the genre is in the database.
            for (var attempt = 1; ; attempt++)
            {
                var movie = new Movie { Id = movieId, Title = details.Title, PosterPath = details.PosterPath, TmdbVoteAverage = details.VoteAverage };
                movie.Translations.Add(new MovieTranslation { Language = language, Title = details.Title });
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
                            entry.Entity is MovieTranslation { MovieId: var tid } && tid == movieId ||
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

        // Best effort: if TMDb cannot be reached the fallback title is shown and
        // the worker tries again later.
        private async Task TryAddTranslationAsync(Movie movie, string language)
        {
            if (await HasTranslationAsync(movie.Id, language)) return;
            try
            {
                var details = await FetchFromTmdbAsync(movie.Id, language);
                await SaveTranslationAsync(movie.Id, language, details.Title);
            }
            catch (Exception ex) when (ex is DomainException or HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogInformation("Could not fetch the {Language} title of movie {MovieId}: {Error}", language, movie.Id, ex.Message);
            }
        }

        // The details page (MoviesController) already has the TMDb response in
        // the request language: store the title if the movie is in the catalog
        // and has none in that language yet.
        public async Task RememberTitleAsync(int movieId, string language, string detailsJson)
        {
            if (await HasTranslationAsync(movieId, language) || !await _context.Movies.AnyAsync(m => m.Id == movieId)) return;
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty("title", out var t) && t.GetString() is { Length: > 0 } title)
            {
                await SaveTranslationAsync(movieId, language, title);
            }
        }

        // MovieTranslationWorker: fetches the title in a language. A movie that is
        // not on TMDb gets its fallback title, so it is not asked again.
        public async Task FillTranslationAsync(int movieId, string language)
        {
            if (await HasTranslationAsync(movieId, language)) return;
            string title;
            try
            {
                title = (await FetchFromTmdbAsync(movieId, language)).Title;
            }
            catch (DomainException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
            {
                title = await _context.Movies.Where(m => m.Id == movieId).Select(m => m.Title).FirstAsync();
            }
            await SaveTranslationAsync(movieId, language, title);
        }

        // The movie's title in the language, or its fallback title.
        public async Task<string> TitleAsync(Movie movie, string language) =>
            await _context.MovieTranslations
                .Where(t => t.MovieId == movie.Id && t.Language == language)
                .Select(t => t.Title)
                .FirstOrDefaultAsync() ?? movie.Title;

        private Task<bool> HasTranslationAsync(int movieId, string language) =>
            _context.MovieTranslations.AnyAsync(t => t.MovieId == movieId && t.Language == language);

        // Saved on its own, outside the caller's pending changes; a concurrent
        // insert of the same title is not an error.
        private Task SaveTranslationAsync(int movieId, string language, string title) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO ""MovieTranslations"" (""MovieId"", ""Language"", ""Title"", ""UpdatedAt"")
                   VALUES ({movieId}, {language}, {title}, {DateTime.UtcNow})
                   ON CONFLICT DO NOTHING");

        // Fills in the genres of a movie that has none from TMDb (GenreBackfillWorker).
        // A movie without genres on TMDb also counts as processed; it is not asked again.
        public async Task SyncGenresAsync(int movieId)
        {
            var movie = await _context.Movies.Include(m => m.MovieGenres).FirstOrDefaultAsync(m => m.Id == movieId);
            if (movie == null) return;

            // No request here: the default language; its title is kept as well.
            var details = await FetchFromTmdbAsync(movieId, SupportedLanguages.Default);
            if (movie.MovieGenres.Count == 0) await AttachGenresAsync(movie, details.Genres);
            movie.GenresSyncedAt = DateTime.UtcNow;
            movie.TmdbVoteAverage = details.VoteAverage;
            await _context.SaveChangesAsync();
            await SaveTranslationAsync(movieId, SupportedLanguages.Default, details.Title);
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

        private async Task<(string Title, string? PosterPath, IReadOnlyList<(int Id, string Name)> Genres, double? VoteAverage)> FetchFromTmdbAsync(int movieId, string language)
        {
            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                throw new DomainException(
                    "movies.tmdbNotConfigured",
                    StatusCodes.Status503ServiceUnavailable);
            }

            // Movie details are cached for 24 hours (same key as MoviesController).
            var cacheKey = DetailsCacheKey(language, movieId);
            if (!_cache.TryGetValue(cacheKey, out string? json) || json == null)
            {
                var client = _httpClientFactory.CreateClient("TMDb");
                var url = DetailsUrl(_tmdbApiKey, language, movieId);
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

            return (string.IsNullOrWhiteSpace(title) ? UntitledTitle(language) : title, posterPath, genres, voteAverage);
        }
    }
}
