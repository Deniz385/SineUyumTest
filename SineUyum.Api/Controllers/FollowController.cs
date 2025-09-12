using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Hubs;
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
        private readonly IHubContext<NotificationHub> _hubContext;

        public FollowController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost("{userIdToFollow}")]
        public async Task<IActionResult> Follow(string userIdToFollow)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (currentUserId == userIdToFollow)
            {
                return BadRequest("Kullanıcı kendini takip edemez.");
            }
             var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null) return Unauthorized();

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

            // --- BİLDİRİM OLUŞTURMA VE GÖNDERME ---
            var notification = new Notification
            {
                UserId = userIdToFollow, // Bildirim, takip edilen kişiye gidecek
                Message = $"{currentUser.UserName} sizi takip etmeye başladı.",
                RelatedUrl = $"/profile/{currentUserId}" // Tıklayınca takip edenin profiline gitsin
            };
            await _context.Notifications.AddAsync(notification);

            await _context.SaveChangesAsync();
            
            // SignalR ile anlık bildirimi gönder
            await _hubContext.Clients.User(userIdToFollow).SendAsync("ReceiveNotification", notification);

            return Ok(new { message = "Kullanıcı başarıyla takip edildi." });
        }

        // ... Diğer metodlar (Unfollow, GetFollowStatus, GetFollowers, GetFollowing) aynı kalacak ...
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

