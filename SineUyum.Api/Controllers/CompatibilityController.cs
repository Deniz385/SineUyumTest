using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Localization;
using SineUyum.Api.RateLimiting;
using SineUyum.Api.Services;
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
        private readonly PrivacyService _privacy;
        private readonly IStringLocalizer _t;

        public CompatibilityController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, PrivacyService privacy, IStringLocalizer localizer)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _privacy = privacy;
            _t = localizer;
            // An empty string must count as unset too; otherwise the placeholder
            // in appsettings.json shadowed the TMDB_API_KEY environment variable.
            var apiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = configuration["TMDB_API_KEY"];
            }
            _tmdbApiKey = apiKey;
        }

        [HttpGet("{targetUserId}")]
        public async Task<ActionResult<CompatibilityDto>> GetCompatibility(string targetUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            // Compatibility is derived from the target's ratings; same rule as profile details.
            await _privacy.EnsureCanViewDetailsAsync(currentUserId, targetUserId, User.IsInRole("Admin"));

            var lang = SupportedLanguages.Current;
            var commonRatingsQuery = from r1 in _context.UserRatings.Where(r => r.UserId == currentUserId)
                                     join r2 in _context.UserRatings.Where(r => r.UserId == targetUserId) on r1.MovieId equals r2.MovieId
                                     join movie in _context.Movies on r1.MovieId equals movie.Id
                                     select new CommonMovieDto
                                     {
                                         MovieId = r1.MovieId,
                                         Title = movie.Translations.Where(t => t.Language == lang).Select(t => t.Title).FirstOrDefault() ?? movie.Title,
                                         PosterPath = movie.Translations.Where(t => t.Language == lang).Select(t => t.PosterPath).FirstOrDefault() ?? movie.PosterPath,
                                         CurrentUserRating = r1.Rating,
                                         TargetUserRating = r2.Rating
                                     };
            var commonMovies = await commonRatingsQuery.ToListAsync();

            // Per-genre averages use all ratings (not only the common ones), so
            // "you both love drama" can be said even with few common movies.
            var tastes = await (
                from r in _context.UserRatings
                where r.UserId == currentUserId || r.UserId == targetUserId
                join mg in _context.MovieGenres on r.MovieId equals mg.MovieId
                group r by new { r.UserId, mg.GenreId, mg.Genre.Name } into g
                select new { g.Key.UserId, g.Key.GenreId, g.Key.Name, Average = g.Average(x => (double)x.Rating), Count = g.Count() })
                .ToListAsync();
            List<GenreTaste> TastesOf(string userId) => tastes
                .Where(t => t.UserId == userId)
                .Select(t => new GenreTaste(t.GenreId, t.Name, t.Average, t.Count))
                .ToList();

            var ratingPairs = commonMovies
                .Select(m => (m.CurrentUserRating, m.TargetUserRating))
                .ToList();

            var result = new CompatibilityDto
            {
                CompatibilityScore = commonMovies.Count == 0 ? 0 : Math.Round(CompatibilityCalculator.Score(ratingPairs), 2),
                CommonMovieCount = commonMovies.Count,
                CommonMovies = commonMovies,
                Insights = CompatibilityInsights.Build(commonMovies, TastesOf(currentUserId), TastesOf(targetUserId), _t)
            };

            return Ok(result);
        }

        [HttpGet("{targetUserId}/recommendations")]
        [EnableRateLimiting(RateLimitPolicies.Tmdb)]
        public async Task<IActionResult> GetJointRecommendations(string targetUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            // Compatibility is derived from the target's ratings; same rule as profile details.
            await _privacy.EnsureCanViewDetailsAsync(currentUserId, targetUserId, User.IsInRole("Admin"));

            if (string.IsNullOrEmpty(_tmdbApiKey))
            {
                return StatusCode(503, _t["errors.tmdbNotConfigured"].Value);
            }

            var currentUserFavoriteGenres = await GetFavoriteGenreIds(currentUserId, 3);
            var targetUserFavoriteGenres = await GetFavoriteGenreIds(targetUserId, 3);

            var jointFavoriteGenres = currentUserFavoriteGenres.Intersect(targetUserFavoriteGenres).ToList();
            if (jointFavoriteGenres.Count == 0)
            {
                jointFavoriteGenres = currentUserFavoriteGenres.Union(targetUserFavoriteGenres).Distinct().Take(3).ToList();
            }

            // TMDb results (titles, overviews) in the request language.
            var language = SupportedLanguages.TmdbLanguage(SupportedLanguages.Current);
            var client = _httpClientFactory.CreateClient("TMDb");
            if (jointFavoriteGenres.Count == 0)
            {
                var popularMoviesResponse = await client.GetAsync($"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language={language}&page=1");
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
            var discoverUrl = $"{TmdbApiBaseUrl}discover/movie?api_key={_tmdbApiKey}&language={language}&sort_by=popularity.desc&with_genres={genresQueryParam}";

            var discoverResponse = await client.GetAsync(discoverUrl);
            if (!discoverResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)discoverResponse.StatusCode, _t["errors.tmdbRecommendationsFailed"].Value);
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
