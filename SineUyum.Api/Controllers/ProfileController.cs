// SineUyum.Api/Controllers/ProfileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ProfileController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        [HttpGet("{userId}")]
        public async Task<ActionResult<UserProfileDto>> GetUserProfile(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound("Kullanıcı bulunamadı.");
            }


            var ratingsQuery = _context.UserRatings.Where(r => r.UserId == userId);

            // İstatistikler veritabanında hesaplanır — tüm puanlar belleğe
            // çekilmez (M5). Sayı ve ortalama tek sorguyla.
            var totalRatings = await ratingsQuery.CountAsync();
            var averageRating = totalRatings == 0
                ? 0
                : Math.Round(await ratingsQuery.AverageAsync(r => (double)r.Rating), 1);

            // Döndürülen liste sınırlı: en yüksek puanlı ilk 100. Bir kullanıcının
            // binlerce puanı olsa bile yanıt ve bellek sınırlı kalır.
            const int maxRatingsReturned = 100;
            var userRatings = await ratingsQuery
                .OrderByDescending(r => r.Rating)
                .ThenByDescending(r => r.MovieId)
                .Take(maxRatingsReturned)
                .Select(r => new ProfileRatingDto
                {
                    MovieId = r.MovieId,
                    Rating = r.Rating,
                    Title = r.Movie.Title,
                    PosterPath = r.Movie.PosterPath
                })
                .ToListAsync();

            var totalMoviesInWatchlists = await _context.Watchlists
                .Where(w => w.UserId == userId)
                .SelectMany(w => w.Items)
                .CountAsync();

            var stats = new ProfileStatisticsDto
            {
                TotalRatings = totalRatings,
                AverageRating = averageRating,
                TotalMoviesInWatchlists = totalMoviesInWatchlists,
                // En yüksek puanlı (10) filmlerden ilk 5'i (liste zaten puana göre sıralı).
                TopRatedMovies = userRatings.Where(r => r.Rating == 10).Take(5).ToList()
            };

            var profileData = new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                Ratings = userRatings,
                Statistics = stats
            };

            return Ok(profileData);
        }

        [HttpPut]
        public async Task<ActionResult<StatusMessageDto>> UpdateProfile([FromBody] UpdateProfileDto updateDto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user == null)
            {
                return NotFound("Kullanıcı bulunamadı.");
            }

            user.Bio = updateDto.Bio;
            user.ProfileImageUrl = updateDto.ProfileImageUrl;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok(new StatusMessageDto("Profil başarıyla güncellendi."));
        }
    }
}