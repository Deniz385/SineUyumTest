using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // Engelleme (G12). Etkisi iki yönlüdür; kurallar PrivacyService'te.
    [ApiController]
    [Route("api/blocks")]
    [Authorize]
    public class BlocksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BlocksController(ApplicationDbContext context)
        {
            _context = context;
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

        // Tekrarlanabilir: zaten engelliyse yine 200.
        [HttpPost("{userId}")]
        public async Task<ActionResult<StatusMessageDto>> Block(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (currentUserId == userId) return BadRequest(new StatusMessageDto("Kendinizi engelleyemezsiniz."));
            if (!await _context.Users.AnyAsync(u => u.Id == userId)) return NotFound(new StatusMessageDto("Kullanıcı bulunamadı."));

            if (!await _context.UserBlocks.AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == userId))
            {
                _context.UserBlocks.Add(new UserBlock { BlockerId = currentUserId, BlockedId = userId });
            }

            // Engel takip ilişkisini iki yönde de bitirir; engel kalkınca geri gelmez.
            var follows = await _context.UserFollows
                .Where(f => (f.FollowerId == currentUserId && f.FollowingId == userId) ||
                            (f.FollowerId == userId && f.FollowingId == currentUserId))
                .ToListAsync();
            _context.UserFollows.RemoveRange(follows);

            // Ortak listeler de biter: birinin sahibi olduğu listede diğeri üye kalmaz.
            var memberships = await _context.WatchlistMembers
                .Where(m => (m.UserId == userId && m.Watchlist.UserId == currentUserId) ||
                            (m.UserId == currentUserId && m.Watchlist.UserId == userId))
                .ToListAsync();
            _context.WatchlistMembers.RemoveRange(memberships);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Eşzamanlı ikinci istek aynı kaydı eklemişse sonuç aynı; değilse gerçek hata.
                if (!await IsBlockedAsync(currentUserId, userId)) throw;
            }

            return Ok(new StatusMessageDto("Kullanıcı engellendi."));
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

            return Ok(new StatusMessageDto("Engel kaldırıldı."));
        }

        private async Task<bool> IsBlockedAsync(string blockerId, string blockedId)
        {
            _context.ChangeTracker.Clear();
            return await _context.UserBlocks.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId);
        }
    }
}
