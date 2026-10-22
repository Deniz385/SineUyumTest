using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SineUyum.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.RateLimiting;
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
        private readonly IStringLocalizer _t;

        public FollowController(ApplicationDbContext context, NotificationService notifications, PrivacyService privacy, IStringLocalizer localizer)
        {
            _context = context;
            _notifications = notifications;
            _privacy = privacy;
            _t = localizer;
        }

        [HttpPost("{userIdToFollow}")]
        // Follows create notifications; a strict limit against follow/unfollow spam.
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<StatusMessageDto>> Follow(string userIdToFollow)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (currentUserId == userIdToFollow)
            {
                return BadRequest(_t["follows.cannotFollowSelf"].Value);
            }
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null) return Unauthorized();

            // A missing user used to give a 500 via the FK error.
            if (!await _context.Users.AnyAsync(u => u.Id == userIdToFollow))
            {
                return NotFound(_t["errors.userNotFound"].Value);
            }
            if (await _privacy.IsBlockedEitherWayAsync(currentUserId, userIdToFollow))
            {
                throw new DomainException("follows.notAllowed", StatusCodes.Status403Forbidden);
            }

            var alreadyFollowing = await _context.UserFollows
                .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == userIdToFollow);

            if (alreadyFollowing)
            {
                return BadRequest(_t["follows.alreadyFollowing"].Value);
            }

            var follow = new UserFollow
            {
                FollowerId = currentUserId,
                FollowingId = userIdToFollow
            };
            await _context.UserFollows.AddAsync(follow);

            // The follow and the followed user's notification are saved
            // together; then SignalR + push. Tapping it opens the follower's profile.
            await _notifications.NotifyAsync(
                userIdToFollow,
                $"/profile/{currentUserId}",
                "notifications.newFollower", currentUser.UserName);

            return Ok(new StatusMessageDto(_t["follows.followed"]));
        }

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
                return NotFound(_t["follows.notFound"].Value);
            }

            _context.UserFollows.Remove(follow);
            await _context.SaveChangesAsync();

            return Ok(new StatusMessageDto(_t["follows.unfollowed"]));
        }

        // GET: api/follow/{userId}/status
        [HttpGet("{userId}/status")]
        public async Task<ActionResult<FollowStatusDto>> GetFollowStatus(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (await _privacy.HasBlockedAsync(userId, currentUserId))
            {
                return NotFound(_t["errors.userNotFound"].Value);
            }

            var isFollowing = await _context.UserFollows
                .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);

            var followerCount = await _context.UserFollows.CountAsync(f => f.FollowingId == userId);
            var followingCount = await _context.UserFollows.CountAsync(f => f.FollowerId == userId);

            return Ok(new FollowStatusDto { IsFollowing = isFollowing, FollowerCount = followerCount, FollowingCount = followingCount });
        }

        // GET: api/follow/{userId}/followers
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

        // GET: api/follow/{userId}/following
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

