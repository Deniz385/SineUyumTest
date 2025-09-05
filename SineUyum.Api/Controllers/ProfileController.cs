using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

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

        // GET: /api/profile/{userId}
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserProfile(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound("Kullanıcı bulunamadı.");
            }

            // Kullanıcının oyladığı filmleri, film bilgileriyle birlikte çekiyoruz.
            // Include(r => r.Movie) sayesinde ilgili filmin tüm detaylarını da alıyoruz.
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
                Ratings = userRatings
            };

            return Ok(profileData);
        }
    }
}