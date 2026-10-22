// SineUyum.Api/Controllers/MoviesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Localization;
using SineUyum.Api.RateLimiting;
using SineUyum.Api.Services;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    // Every endpoint may hit TMDb (cache miss); a strict limit protects the quota.
    [EnableRateLimiting(RateLimitPolicies.Tmdb)]
    public class MoviesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly string? _tmdbApiKey;
        private readonly IStringLocalizer _t;
        private readonly MovieCatalogService _catalog;
        private readonly ILogger<MoviesController> _logger;
        private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3/";

        public MoviesController(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IConfiguration configuration,
            IStringLocalizer localizer,
            MovieCatalogService catalog,
            ILogger<MoviesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _t = localizer;
            _catalog = catalog;
            _logger = logger;

            var apiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = configuration["TMDB_API_KEY"];
            }
            _tmdbApiKey = apiKey;
        }

        // TMDb's language for the request culture ("tr-TR", "en-US"). Part of
        // every cache key so different languages never mix.
        private static string Language => SupportedLanguages.TmdbLanguage(SupportedLanguages.Current);

        [HttpGet("search")]
        public Task<IActionResult> SearchMovies([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult<IActionResult>(BadRequest(_t["movies.searchQueryRequired"].Value));
            }

            // Search results are cached per query with a relatively short TTL.
            var url = $"{TmdbApiBaseUrl}search/movie?api_key={_tmdbApiKey}&query={Uri.EscapeDataString(query)}&language={Language}&page=1";
            return CachedTmdbJson($"search:{Language}:{query.ToLowerInvariant()}", url, TimeSpan.FromMinutes(30));
        }

        [HttpGet("{movieId}")]
        public Task<IActionResult> GetMovieById(int movieId)
        {
            // Movie details rarely change; long TTL. Same request and key as
            // MovieCatalogService. The title is remembered for the catalog if
            // the movie is stored but has none in this language yet.
            var language = SupportedLanguages.Current;
            var url = MovieCatalogService.DetailsUrl(_tmdbApiKey ?? string.Empty, language, movieId);
            return CachedTmdbJson(MovieCatalogService.DetailsCacheKey(language, movieId), url, TimeSpan.FromHours(24),
                json => RememberTitleAsync(movieId, language, json));
        }

        private async Task RememberTitleAsync(int movieId, string language, string json)
        {
            try
            {
                await _catalog.RememberTitleAsync(movieId, language, json);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or System.Data.Common.DbException)
            {
                // Nice to have only: the details page must not fail because of it.
                _logger.LogWarning(ex, "Could not store the {Language} title of movie {MovieId}.", language, movieId);
            }
        }

        [HttpGet("popular")]
        public Task<IActionResult> GetPopularMovies()
        {
            // The same for every user of a language; changes slowly.
            var url = $"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language={Language}&page=1";
            return CachedTmdbJson($"popular:{Language}", url, TimeSpan.FromHours(1));
        }

        [HttpGet("now_playing")]
        public Task<IActionResult> GetNowPlayingMovies()
        {
            var url = $"{TmdbApiBaseUrl}movie/now_playing?api_key={_tmdbApiKey}&language={Language}&page=1";
            return CachedTmdbJson($"now_playing:{Language}", url, TimeSpan.FromHours(1));
        }

        // Fetches JSON from TMDb, or returns it from the cache. Errors (missing
        // key, TMDb error, unreachable) are not cached and are passed through.
        private async Task<IActionResult> CachedTmdbJson(string cacheKey, string url, TimeSpan ttl, Func<string, Task>? onJson = null)
        {
            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                return StatusCode(500, _t["errors.tmdbNotConfigured"].Value);
            }

            if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            {
                if (onJson != null) await onJson(cached);
                return Content(cached, "application/json");
            }

            var client = _httpClientFactory.CreateClient("TMDb");
            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, _t["errors.tmdbRequestFailed"].Value);
                }

                var json = await response.Content.ReadAsStringAsync();
                _cache.Set(cacheKey, json, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    Size = 1, // each entry counts as 1 unit towards SizeLimit
                });
                if (onJson != null) await onJson(json);
                return Content(json, "application/json");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, _t["errors.serviceUnreachable", ex.Message].Value);
            }
        }
    }
}
