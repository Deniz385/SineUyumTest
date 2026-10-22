using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;

namespace SineUyum.Api.Services
{
    // The single home of the privacy rules (G12): profile, follow, message,
    // search and matching all take their decisions from here.
    public class PrivacyService
    {
        private readonly ApplicationDbContext _context;

        public PrivacyService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Is there a block between the two users in either direction?
        public Task<bool> IsBlockedEitherWayAsync(string userId, string otherUserId) =>
            _context.UserBlocks.AnyAsync(b =>
                (b.BlockerId == userId && b.BlockedId == otherUserId) ||
                (b.BlockerId == otherUserId && b.BlockedId == userId));

        public Task<bool> HasBlockedAsync(string blockerId, string blockedId) =>
            _context.UserBlocks.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId);

        // Everyone the user blocked and everyone who blocked them (to filter queries).
        public IQueryable<string> BlockedPartnerIds(string userId) =>
            _context.UserBlocks.Where(b => b.BlockerId == userId).Select(b => b.BlockedId)
                .Concat(_context.UserBlocks.Where(b => b.BlockedId == userId).Select(b => b.BlockerId));

        // Can the viewer see profile details (ratings, statistics, follow lists,
        // compatibility)? Blocks are not this method's concern; the caller checks them first.
        public async Task<bool> CanViewDetailsAsync(string viewerId, AppUser owner, bool viewerIsAdmin)
        {
            if (viewerId == owner.Id || viewerIsAdmin) return true;
            return owner.ProfileVisibility switch
            {
                ProfileVisibility.Public => true,
                ProfileVisibility.Following => await FollowsAsync(owner.Id, viewerId),
                _ => false
            };
        }

        // For endpoints that need profile details: 404 on a block (not even the
        // existence leaks), 403 on a private profile.
        public async Task<AppUser> EnsureCanViewDetailsAsync(string viewerId, string ownerId, bool viewerIsAdmin)
        {
            var owner = await _context.Users.FindAsync(ownerId);
            if (owner == null || (viewerId != ownerId && await IsBlockedEitherWayAsync(viewerId, ownerId)))
            {
                throw DomainException.NotFound("errors.userNotFound");
            }
            if (!await CanViewDetailsAsync(viewerId, owner, viewerIsAdmin))
            {
                throw new DomainException("privacy.profileDetailsHidden", StatusCodes.Status403Forbidden);
            }
            return owner;
        }

        // Can a new message be sent? The past conversation stays readable.
        public async Task EnsureCanMessageAsync(string senderId, AppUser recipient)
        {
            if (await IsBlockedEitherWayAsync(senderId, recipient.Id))
            {
                throw new DomainException("privacy.cannotMessageUser", StatusCodes.Status403Forbidden);
            }

            var allowed = recipient.MessagePermission switch
            {
                MessagePermission.Everyone => true,
                MessagePermission.Following => await FollowsAsync(recipient.Id, senderId),
                _ => false
            };
            if (!allowed)
            {
                throw new DomainException(
                    recipient.MessagePermission == MessagePermission.Following
                        ? "privacy.messagesFromFollowingOnly"
                        : "privacy.messagesDisabled",
                    StatusCodes.Status403Forbidden);
            }
        }

        // Block pairs among the given users (for matching), added in both directions.
        public async Task<HashSet<(string, string)>> BlockedPairsAmongAsync(IReadOnlyCollection<string> userIds)
        {
            var blocks = await _context.UserBlocks
                .Where(b => userIds.Contains(b.BlockerId) && userIds.Contains(b.BlockedId))
                .Select(b => new { b.BlockerId, b.BlockedId })
                .ToListAsync();

            var pairs = new HashSet<(string, string)>();
            foreach (var b in blocks)
            {
                pairs.Add((b.BlockerId, b.BlockedId));
                pairs.Add((b.BlockedId, b.BlockerId));
            }
            return pairs;
        }

        private Task<bool> FollowsAsync(string followerId, string followingId) =>
            _context.UserFollows.AnyAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);
    }
}
