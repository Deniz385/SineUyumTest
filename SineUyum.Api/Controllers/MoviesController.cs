// SineUyum.Api/Controllers/MoviesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using SineUyum.Api.RateLimiting;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    // Her uç TMDb'ye gidebilir (önbellek ıskası); kotayı korumak için sıkı sınır.
    [EnableRateLimiting(RateLimitPolicies.Tmdb)]
    public class MoviesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly string? _tmdbApiKey;
        private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3/";

        public MoviesController(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;

            var apiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = configuration["TMDB_API_KEY"];
            }
            _tmdbApiKey = apiKey;
        }

        [HttpGet("search")]
        public Task<IActionResult> SearchMovies([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult<IActionResult>(BadRequest("Arama metni boş olamaz."));
            }

            // Arama sonuçları sorgu başına, görece kısa TTL ile önbelleklenir.
            var url = $"{TmdbApiBaseUrl}search/movie?api_key={_tmdbApiKey}&query={Uri.EscapeDataString(query)}&language=tr-TR&page=1";
            return CachedTmdbJson($"search:{query.ToLowerInvariant()}", url, TimeSpan.FromMinutes(30));
        }

        [HttpGet("{movieId}")]
        public Task<IActionResult> GetMovieById(int movieId)
        {
            // Film detayı nadiren değişir; uzun TTL.
            var url = $"{TmdbApiBaseUrl}movie/{movieId}?api_key={_tmdbApiKey}&language=tr-TR&append_to_response=credits";
            return CachedTmdbJson($"movie:{movieId}", url, TimeSpan.FromHours(24));
        }

        [HttpGet("popular")]
        public Task<IActionResult> GetPopularMovies()
        {
            // Tüm kullanıcılar için aynı; yavaş değişir.
            var url = $"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language=tr-TR&page=1";
            return CachedTmdbJson("popular", url, TimeSpan.FromHours(1));
        }

        [HttpGet("now_playing")]
        public Task<IActionResult> GetNowPlayingMovies()
        {
            var url = $"{TmdbApiBaseUrl}movie/now_playing?api_key={_tmdbApiKey}&language=tr-TR&page=1";
            return CachedTmdbJson("now_playing", url, TimeSpan.FromHours(1));
        }

        // TMDb'den JSON çeker; cache'te varsa oradan döner. Hatalar (eksik
        // anahtar, TMDb hatası, erişilemezlik) önbelleğe alınmaz, olduğu gibi
        // yansıtılır.
        private async Task<IActionResult> CachedTmdbJson(string cacheKey, string url, TimeSpan ttl)
        {
            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                return StatusCode(500, "TMDb API anahtarı yapılandırılmamış.");
            }

            if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            {
                return Content(cached, "application/json");
            }

            var client = _httpClientFactory.CreateClient("TMDb");
            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "TMDb API'sinden veri alınamadı.");
                }

                var json = await response.Content.ReadAsStringAsync();
                _cache.Set(cacheKey, json, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    Size = 1, // SizeLimit için her girdi 1 birim sayılır
                });
                return Content(json, "application/json");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, $"Servise erişilemiyor: {ex.Message}");
            }
        }
    }
}
