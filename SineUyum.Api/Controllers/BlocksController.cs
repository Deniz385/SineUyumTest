using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // Blocking (G12). It works both ways; the rules live in PrivacyService.
    [ApiController]
    [Route("api/blocks")]
    [Authorize]
    public class BlocksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer _t;

        public BlocksController(ApplicationDbContext context, IStringLocalizer localizer)
        {
            _context = context;
            _t = localizer;
        }

        [HttpGet]
        public async Task<ActionResult<List<BlockedUserDto>>> GetBlocked()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var blocked = await _context.UserBlocks
                .Where(b => b.BlockerId == currentUserId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlockedUserDto
                {
                    Id = b.BlockedId,
                    UserName = b.Blocked.UserName,
                    ProfileImageUrl = b.Blocked.ProfileImageUrl,
                    BlockedAt = b.CreatedAt
                })
                .ToListAsync();

            return Ok(blocked);
        }

        // Idempotent: blocking again still returns 200.
        [HttpPost("{userId}")]
        public async Task<ActionResult<StatusMessageDto>> Block(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (currentUserId == userId) return BadRequest(new StatusMessageDto(_t["blocks.cannotBlockSelf"]));
            if (!await _context.Users.AnyAsync(u => u.Id == userId)) return NotFound(new StatusMessageDto(_t["errors.userNotFound"]));

            if (!await _context.UserBlocks.AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == userId))
            {
                _context.UserBlocks.Add(new UserBlock { BlockerId = currentUserId, BlockedId = userId });
            }

            // A block ends following in both directions; it does not come back on unblock.
            var follows = await _context.UserFollows
                .Where(f => (f.FollowerId == currentUserId && f.FollowingId == userId) ||
                            (f.FollowerId == userId && f.FollowingId == currentUserId))
                .ToListAsync();
            _context.UserFollows.RemoveRange(follows);

            // Shared lists end too: neither stays a member of a list the other owns.
            var memberships = await _context.WatchlistMembers
                .Where(m => (m.UserId == userId && m.Watchlist.UserId == currentUserId) ||
                            (m.UserId == currentUserId && m.Watchlist.UserId == userId))
                .ToListAsync();
            _context.WatchlistMembers.RemoveRange(memberships);

            // Likes and saves of each other's lists go as well.
            _context.WatchlistLikes.RemoveRange(await _context.WatchlistLikes
                .Where(l => (l.UserId == userId && l.Watchlist.UserId == currentUserId) ||
                            (l.UserId == currentUserId && l.Watchlist.UserId == userId))
                .ToListAsync());
            _context.WatchlistSaves.RemoveRange(await _context.WatchlistSaves
                .Where(v => (v.UserId == userId && v.Watchlist.UserId == currentUserId) ||
                            (v.UserId == currentUserId && v.Watchlist.UserId == userId))
                .ToListAsync());

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent second request added the same row: same outcome. Otherwise a real error.
                if (!await IsBlockedAsync(currentUserId, userId)) throw;
            }

            return Ok(new StatusMessageDto(_t["blocks.blocked"]));
        }

        [HttpDelete("{userId}")]
        public async Task<ActionResult<StatusMessageDto>> Unblock(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var block = await _context.UserBlocks
                .FirstOrDefaultAsync(b => b.BlockerId == currentUserId && b.BlockedId == userId);
            if (block != null)
            {
                _context.UserBlocks.Remove(block);
                await _context.SaveChangesAsync();
            }

            return Ok(new StatusMessageDto(_t["blocks.unblocked"]));
        }

        private async Task<bool> IsBlockedAsync(string blockerId, string blockedId)
        {
            _context.ChangeTracker.Clear();
            return await _context.UserBlocks.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId);
        }
    }
}
