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
        public async Task<IActionResult> GetUserProfile(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound("Kullanıcı bulunamadı.");
            }


            var userRatings = await _context.UserRatings
                .Where(r => r.UserId == userId)
                .Include(r => r.Movie) 
                .OrderByDescending(r => r.Rating) 
                .Select(r => new 
                {
                    r.MovieId,
                    r.Rating,
                    r.Movie.Title,
                    r.Movie.PosterPath
                })
                .ToListAsync();

            var totalMoviesInWatchlists = await _context.Watchlists
                .Where(w => w.UserId == userId)
                .SelectMany(w => w.Items)
                .CountAsync();

            var stats = new {
                totalRatings = userRatings.Count,
                averageRating = userRatings.Any() ? Math.Round(userRatings.Average(r => r.Rating), 1) : 0,
                totalMoviesInWatchlists = totalMoviesInWatchlists,
                // En yüksek puanlı (10) filmlerden ilk 5'ini al
                topRatedMovies = userRatings.Where(r => r.Rating == 10).Take(5).ToList() 
            };

            var profileData = new 
            {
                user.Id,
                user.UserName,
                user.Bio,
                user.ProfileImageUrl,
                Ratings = userRatings, 
                Statistics = stats 
            };

            return Ok(profileData);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto updateDto)
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

            return Ok(new { message = "Profil başarıyla güncellendi." });
        }
    }
}