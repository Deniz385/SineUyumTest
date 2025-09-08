// SineUyum.Api/Controllers/FollowController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FollowController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FollowController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/follow/{userIdToFollow}
        [HttpPost("{userIdToFollow}")]
        public async Task<IActionResult> Follow(string userIdToFollow)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (currentUserId == userIdToFollow)
            {
                return BadRequest("Kullanıcı kendini takip edemez.");
            }

            var alreadyFollowing = await _context.UserFollows
                .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == userIdToFollow);

            if (alreadyFollowing)
            {
                return BadRequest("Bu kullanıcıyı zaten takip ediyorsunuz.");
            }

            var follow = new UserFollow
            {
                FollowerId = currentUserId,
                FollowingId = userIdToFollow
            };

            await _context.UserFollows.AddAsync(follow);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcı başarıyla takip edildi." });
        }

        // DELETE: api/follow/{userIdToUnfollow}
        [HttpDelete("{userIdToUnfollow}")]
        public async Task<IActionResult> Unfollow(string userIdToUnfollow)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var follow = await _context.UserFollows
                .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowingId == userIdToUnfollow);

            if (follow == null)
            {
                return NotFound("Takip ilişkisi bulunamadı.");
            }

            _context.UserFollows.Remove(follow);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcı takipten çıkarıldı." });
        }

        // GET: api/follow/{userId}/status
        [HttpGet("{userId}/status")]
        public async Task<IActionResult> GetFollowStatus(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var isFollowing = await _context.UserFollows
                .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);

            var followerCount = await _context.UserFollows.CountAsync(f => f.FollowingId == userId);
            var followingCount = await _context.UserFollows.CountAsync(f => f.FollowerId == userId);

            return Ok(new { isFollowing, followerCount, followingCount });
        }

        // --- YENİ EKLENEN METOT: Takipçileri Getir ---
        [HttpGet("{userId}/followers")]
        public async Task<IActionResult> GetFollowers(string userId)
        {
            var followers = await _context.UserFollows
                .Where(f => f.FollowingId == userId)
                .Include(f => f.Follower) // Takip eden kullanıcının bilgilerini de al
                .Select(f => new { f.Follower.Id, f.Follower.UserName })
                .ToListAsync();
            
            return Ok(followers);
        }

        // --- YENİ EKLENEN METOT: Takip Edilenleri Getir ---
        [HttpGet("{userId}/following")]
        public async Task<IActionResult> GetFollowing(string userId)
        {
            var following = await _context.UserFollows
                .Where(f => f.FollowerId == userId)
                .Include(f => f.Following) // Takip edilen kullanıcının bilgilerini de al
                .Select(f => new { f.Following.Id, f.Following.UserName })
                .ToListAsync();

            return Ok(following);
        }
    }
}