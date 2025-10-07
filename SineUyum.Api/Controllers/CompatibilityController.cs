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
            var apiKey = configuration["TMDb:ApiKey"] ?? configuration["TMDB_API_KEY"];
            _tmdbApiKey = apiKey;
        }

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

            if (!commonMovies.Any())
            {
                return Ok(new
                {
                    compatibilityScore = 0,
                    commonMovieCount = 0,
                    commonMovies = new List<object>()
                });
            }
            
            double totalWeightedScore = 0;
            
            foreach (var movie in commonMovies)
            {
                double difference = Math.Abs(movie.CurrentUserRating - movie.TargetUserRating);
                double weight = 1.0;
                if ((movie.CurrentUserRating <= 3 && movie.TargetUserRating <= 3) || (movie.CurrentUserRating >= 8 && movie.TargetUserRating >= 8))
                {
                    weight = 1.5;
                }
                else if ((movie.CurrentUserRating <= 3 && movie.TargetUserRating >= 8) || (movie.CurrentUserRating >= 8 && movie.TargetUserRating <= 3))
                {
                    weight = 2.0;
                }
                double movieScore = 10 - difference;
                totalWeightedScore += movieScore * weight;
            }

            double maxPossibleScore = commonMovies.Count * 10 * 1.5; 
            double compatibilityScore = (totalWeightedScore / maxPossibleScore) * 100;
            
            var result = new
            {
                compatibilityScore = Math.Round(Math.Max(0, Math.Min(100, compatibilityScore)), 2),
                commonMovieCount = commonMovies.Count,
                commonMovies
            };

            return Ok(result);
        }

        [HttpGet("{targetUserId}/recommendations")]
        public async Task<IActionResult> GetJointRecommendations(string targetUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                return StatusCode(503, "TMDb API anahtarı sunucuda yapılandırılmamış.");
            }
            
            var currentUserFavoriteGenres = await GetFavoriteGenreIds(currentUserId, 3);
            var targetUserFavoriteGenres = await GetFavoriteGenreIds(targetUserId, 3);

            var jointFavoriteGenres = currentUserFavoriteGenres.Intersect(targetUserFavoriteGenres).ToList();
            if (jointFavoriteGenres.Count == 0)
            {
                jointFavoriteGenres = currentUserFavoriteGenres.Union(targetUserFavoriteGenres).Distinct().Take(3).ToList();
            }
            
            var client = _httpClientFactory.CreateClient("TMDb");
            if (jointFavoriteGenres.Count == 0)
            {
                var popularMoviesResponse = await client.GetAsync($"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language=tr-TR&page=1");
                if (popularMoviesResponse.IsSuccessStatusCode)
                {
                     var json = await popularMoviesResponse.Content.ReadAsStringAsync();
                     var popularMovies = JsonDocument.Parse(json).RootElement.GetProperty("results");
                     var ratedMovieIds = await _context.UserRatings.Where(r => r.UserId == currentUserId || r.UserId == targetUserId).Select(r => r.MovieId).ToListAsync();
                     
                     var watchlistMovieIds = await _context.Watchlists
                        .Where(w => w.UserId == currentUserId || w.UserId == targetUserId)
                        .SelectMany(w => w.Items.Select(i => i.MovieId))
                        .ToListAsync();

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
            
            var allWatchlistMovieIds = await _context.Watchlists
                .Where(w => w.UserId == currentUserId || w.UserId == targetUserId)
                .SelectMany(w => w.Items.Select(i => i.MovieId))
                .ToListAsync();

            var allSeenMovieIds = allRatedMovieIds.Union(allWatchlistMovieIds).ToHashSet();

            var finalRecommendations = discoveredMovies.EnumerateArray()
                .Where(m => !allSeenMovieIds.Contains(m.GetProperty("id").GetInt32()))
                .Take(12)
                .ToList();

            return Ok(finalRecommendations);
        }
        
        private async Task<List<int>> GetFavoriteGenreIds(string userId, int count)
        {
            var favoriteMovieIds = await _context.UserRatings
                .Where(r => r.UserId == userId && r.Rating >= 6)
                .Select(r => r.MovieId)
                .ToListAsync();

            if (!favoriteMovieIds.Any())
            {
                return new List<int>();
            }

            var favoriteGenreIds = await _context.MovieGenres
                .Where(mg => favoriteMovieIds.Contains(mg.MovieId))
                .GroupBy(mg => mg.GenreId)
                .Select(g => new
                {
                    GenreId = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .Take(count)
                .Select(g => g.GenreId)
                .ToListAsync();

            return favoriteGenreIds;
        }
    }
}