// SineUyum.Api/Controllers/ProfileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos; // Yeni DTO'yu ekledik
using SineUyum.Api.Models;
using System.Security.Claims; // Claims için eklendi

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

        // GET: /api/profile/{userId} - YENİ ALANLAR EKLENDİ
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
                .Select(r => new 
                {
                    r.MovieId,
                    r.Rating,
                    r.Movie.Title,
                    r.Movie.PosterPath
                })
                .ToListAsync();

            var profileData = new 
            {
                user.Id,
                user.UserName,
                user.Bio, // Bio eklendi
                user.ProfileImageUrl, // Profil fotoğrafı URL'si eklendi
                Ratings = userRatings
            };

            return Ok(profileData);
        }

        // --- YENİ METOT: Profili güncellemek için ---
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