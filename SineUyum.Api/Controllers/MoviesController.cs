// SineUyum.Api/Controllers/MoviesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MoviesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _tmdbApiKey;
        private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3/";

        public MoviesController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            
            var apiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = configuration["TMDB_API_KEY"];
            }
            _tmdbApiKey = apiKey;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchMovies([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Arama metni boş olamaz.");
            }

            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                 return StatusCode(500, "TMDb API anahtarı yapılandırılmamış. Lütfen Codespaces secret'ı kontrol edin.");
            }

            var client = _httpClientFactory.CreateClient("TMDb");
            var searchUrl = $"{TmdbApiBaseUrl}search/movie?api_key={_tmdbApiKey}&query={Uri.EscapeDataString(query)}&language=tr-TR&page=1";

            try
            {
                var response = await client.GetAsync(searchUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return Content(jsonResponse, "application/json");
                }
                else
                {
                    return StatusCode((int)response.StatusCode, "TMDb API'sinden veri alınamadı.");
                }
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, $"Servise erişilemiyor: {ex.Message}");
            }
        }

        [HttpGet("{movieId}")]
        public async Task<IActionResult> GetMovieById(int movieId)
        {
            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                return StatusCode(500, "TMDb API anahtarı yapılandırılmamış.");
            }

            var client = _httpClientFactory.CreateClient("TMDb");
            var movieUrl = $"{TmdbApiBaseUrl}movie/{movieId}?api_key={_tmdbApiKey}&language=tr-TR&append_to_response=credits";

            try
            {
                var response = await client.GetAsync(movieUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return Content(jsonResponse, "application/json");
                }
                else
                {
                    return StatusCode((int)response.StatusCode, "TMDb API'sinden film detayı alınamadı.");
                }
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, $"Servise erişilemiyor: {ex.Message}");
            }
        }

        [HttpGet("popular")]
        public async Task<IActionResult> GetPopularMovies()
        {
            if (string.IsNullOrEmpty(_tmdbApiKey)) return StatusCode(500, "TMDb API anahtarı yapılandırılmamış.");

            var client = _httpClientFactory.CreateClient("TMDb");
            var popularMoviesUrl = $"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language=tr-TR&page=1";

            var response = await client.GetAsync(popularMoviesUrl);
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, "Popüler filmler alınamadı.");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return Content(jsonResponse, "application/json");
        }

        [HttpGet("now_playing")]
        public async Task<IActionResult> GetNowPlayingMovies()
        {
            if (string.IsNullOrEmpty(_tmdbApiKey)) return StatusCode(500, "TMDb API anahtarı yapılandırılmamış.");
            
            var client = _httpClientFactory.CreateClient("TMDb");
            var nowPlayingMoviesUrl = $"{TmdbApiBaseUrl}movie/now_playing?api_key={_tmdbApiKey}&language=tr-TR&page=1";
            
            var response = await client.GetAsync(nowPlayingMoviesUrl);
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, "Vizyondaki filmler alınamadı.");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return Content(jsonResponse, "application/json");
        }
    }
}