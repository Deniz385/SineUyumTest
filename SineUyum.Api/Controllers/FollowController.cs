using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FollowController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly PrivacyService _privacy;

        public FollowController(ApplicationDbContext context, NotificationService notifications, PrivacyService privacy)
        {
            _context = context;
            _notifications = notifications;
            _privacy = privacy;
        }

        [HttpPost("{userIdToFollow}")]
        public async Task<ActionResult<StatusMessageDto>> Follow(string userIdToFollow)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (currentUserId == userIdToFollow)
            {
                return BadRequest("Kullanıcı kendini takip edemez.");
            }
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null) return Unauthorized();

            // Olmayan kullanıcı önceden FK hatasıyla 500 veriyordu.
            if (!await _context.Users.AnyAsync(u => u.Id == userIdToFollow))
            {
                return NotFound("Kullanıcı bulunamadı.");
            }
            if (await _privacy.IsBlockedEitherWayAsync(currentUserId, userIdToFollow))
            {
                throw new DomainException("Bu kullanıcıyı takip edemezsiniz.", StatusCodes.Status403Forbidden);
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

            // Takip ve takip edilenin bildirimi tek kayıtta; ardından SignalR + push.
            // Tıklayınca takip edenin profiline gider.
            await _notifications.NotifyAsync(
                userIdToFollow,
                $"{currentUser.UserName} sizi takip etmeye başladı.",
                $"/profile/{currentUserId}");

            return Ok(new StatusMessageDto("Kullanıcı başarıyla takip edildi."));
        }

        // ... Diğer metodlar (Unfollow, GetFollowStatus, GetFollowers, GetFollowing) aynı kalacak ...
        // DELETE: api/follow/{userIdToUnfollow}
        [HttpDelete("{userIdToUnfollow}")]
        public async Task<ActionResult<StatusMessageDto>> Unfollow(string userIdToUnfollow)
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

            return Ok(new StatusMessageDto("Kullanıcı takipten çıkarıldı."));
        }

        // GET: api/follow/{userId}/status
        [HttpGet("{userId}/status")]
        public async Task<ActionResult<FollowStatusDto>> GetFollowStatus(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (await _privacy.HasBlockedAsync(userId, currentUserId))
            {
                return NotFound("Kullanıcı bulunamadı.");
            }

            var isFollowing = await _context.UserFollows
                .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);

            var followerCount = await _context.UserFollows.CountAsync(f => f.FollowingId == userId);
            var followingCount = await _context.UserFollows.CountAsync(f => f.FollowerId == userId);

            return Ok(new FollowStatusDto { IsFollowing = isFollowing, FollowerCount = followerCount, FollowingCount = followingCount });
        }

        // --- YENİ EKLENEN METOT: Takipçileri Getir ---
        [HttpGet("{userId}/followers")]
        public async Task<ActionResult<List<UserBriefDto>>> GetFollowers(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();
            await _privacy.EnsureCanViewDetailsAsync(currentUserId, userId, User.IsInRole("Admin"));

            var followers = await _context.UserFollows
                .Where(f => f.FollowingId == userId)
                .Select(f => new UserBriefDto { Id = f.Follower.Id, UserName = f.Follower.UserName })
                .ToListAsync();
            
            return Ok(followers);
        }

        // --- YENİ EKLENEN METOT: Takip Edilenleri Getir ---
        [HttpGet("{userId}/following")]
        public async Task<ActionResult<List<UserBriefDto>>> GetFollowing(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();
            await _privacy.EnsureCanViewDetailsAsync(currentUserId, userId, User.IsInRole("Admin"));

            var following = await _context.UserFollows
                .Where(f => f.FollowerId == userId)
                .Select(f => new UserBriefDto { Id = f.Following.Id, UserName = f.Following.UserName })
                .ToListAsync();

            return Ok(following);
        }
    }
}

