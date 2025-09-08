// SineUyum.Api/Controllers/CompatibilityController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using System.Security.Claims;
using System.Text.Json;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CompatibilityController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _tmdbApiKey;
        private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3/";

        public CompatibilityController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;

            // Hem appsettings.json'dan hem de GitHub secrets'dan API anahtarını okuyabilen doğru mantık
            var apiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = configuration["TMDB_API_KEY"];
            }
            _tmdbApiKey = apiKey;
        }

        // Uyum puanı metodu (Bu metodun bir önceki versiyonunda hata vardı, düzeltildi)
        [HttpGet("{targetUserId}")]
        public async Task<IActionResult> GetCompatibility(string targetUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var commonRatingsQuery = from r1 in _context.UserRatings.Where(r => r.UserId == currentUserId)
                                     join r2 in _context.UserRatings.Where(r => r.UserId == targetUserId) on r1.MovieId equals r2.MovieId
                                     join movie in _context.Movies on r1.MovieId equals movie.Id
                                     select new
                                     {
                                         MovieId = r1.MovieId,
                                         Title = movie.Title,
                                         PosterPath = movie.PosterPath,
                                         CurrentUserRating = r1.Rating,
                                         TargetUserRating = r2.Rating
                                     };
            var commonMovies = await commonRatingsQuery.ToListAsync();
            
            // Puan farkına dayalı basit uyum hesaplaması
            double compatibilityScore = 0;
            if (commonMovies.Any())
            {
                double totalDifference = commonMovies.Sum(r => Math.Abs(r.CurrentUserRating - r.TargetUserRating));
                double averageDifference = totalDifference / commonMovies.Count();
                compatibilityScore = 100.0 - (averageDifference / 9.0 * 100.0);
            }

            var result = new
            {
                compatibilityScore = Math.Round(compatibilityScore > 0 ? compatibilityScore : 0, 2),
                commonMovieCount = commonMovies.Count,
                commonMovies
            };

            return Ok(result);
        }

        // Nihai ve çalışan öneri metodu
        [HttpGet("{targetUserId}/recommendations")]
        public async Task<IActionResult> GetJointRecommendations(string targetUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                return StatusCode(503, "TMDb API anahtarı sunucuda yapılandırılmamış.");
            }

            var client = _httpClientFactory.CreateClient("TMDb");
            var currentUserFavoriteGenres = await GetFavoriteGenreIds(currentUserId, 3, client);
            var targetUserFavoriteGenres = await GetFavoriteGenreIds(targetUserId, 3, client);

            var jointFavoriteGenres = currentUserFavoriteGenres.Intersect(targetUserFavoriteGenres).ToList();
            if (jointFavoriteGenres.Count == 0)
            {
                jointFavoriteGenres = currentUserFavoriteGenres.Union(targetUserFavoriteGenres).Distinct().Take(3).ToList();
            }
            
            if (jointFavoriteGenres.Count == 0)
            {
                var popularMoviesResponse = await client.GetAsync($"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language=tr-TR&page=1");
                if (popularMoviesResponse.IsSuccessStatusCode)
                {
                     var json = await popularMoviesResponse.Content.ReadAsStringAsync();
                     var popularMovies = JsonDocument.Parse(json).RootElement.GetProperty("results");
                     var ratedMovieIds = await _context.UserRatings.Where(r => r.UserId == currentUserId || r.UserId == targetUserId).Select(r => r.MovieId).ToListAsync();
                     var watchlistMovieIds = await _context.WatchlistItems.Where(wi => wi.UserId == currentUserId || wi.UserId == targetUserId).Select(wi => wi.MovieId).ToListAsync();
                     var seenMovieIds = ratedMovieIds.Union(watchlistMovieIds).ToHashSet();
                     
                     var filteredPopular = popularMovies.EnumerateArray()
                        .Where(m => !seenMovieIds.Contains(m.GetProperty("id").GetInt32()))
                        .Take(12)
                        .ToList();
                     return Ok(filteredPopular);
                }
                return Ok(new List<object>());
            }

            var genresQueryParam = string.Join(",", jointFavoriteGenres);
            var discoverUrl = $"{TmdbApiBaseUrl}discover/movie?api_key={_tmdbApiKey}&language=tr-TR&sort_by=popularity.desc&with_genres={genresQueryParam}";
            
            var discoverResponse = await client.GetAsync(discoverUrl);
            if (!discoverResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)discoverResponse.StatusCode, "TMDb'den öneri alınamadı.");
            }

            var jsonResponse = await discoverResponse.Content.ReadAsStringAsync();
            var discoveredMovies = JsonDocument.Parse(jsonResponse).RootElement.GetProperty("results");

            var allRatedMovieIds = await _context.UserRatings.Where(r => r.UserId == currentUserId || r.UserId == targetUserId).Select(r => r.MovieId).ToListAsync();
            var allWatchlistMovieIds = await _context.WatchlistItems.Where(wi => wi.UserId == currentUserId || wi.UserId == targetUserId).Select(wi => wi.MovieId).ToListAsync();
            var allSeenMovieIds = allRatedMovieIds.Union(allWatchlistMovieIds).ToHashSet();

            var finalRecommendations = discoveredMovies.EnumerateArray()
                .Where(m => !allSeenMovieIds.Contains(m.GetProperty("id").GetInt32()))
                .Take(12)
                .ToList();

            return Ok(finalRecommendations);
        }

        private async Task<List<int>> GetFavoriteGenreIds(string userId, int count, HttpClient client)
        {
            var favoriteMovieIds = await _context.UserRatings
                .Where(r => r.UserId == userId && r.Rating >= 6)
                .Select(r => r.MovieId)
                .ToListAsync();

            if (!favoriteMovieIds.Any()) return new List<int>();

            var genreCounts = new Dictionary<int, int>();

            foreach (var movieId in favoriteMovieIds.Take(15))
            {
                var movieUrl = $"{TmdbApiBaseUrl}movie/{movieId}?api_key={_tmdbApiKey}&language=tr-TR";
                var response = await client.GetAsync(movieUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var movieDoc = JsonDocument.Parse(jsonResponse).RootElement;
                    if (movieDoc.TryGetProperty("genres", out var genres))
                    {
                        foreach (var genre in genres.EnumerateArray())
                        {
                            var genreId = genre.GetProperty("id").GetInt32();
                            genreCounts[genreId] = genreCounts.GetValueOrDefault(genreId, 0) + 1;
                        }
                    }
                }
            }
            return genreCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).Take(count).ToList();
        }
    }
}