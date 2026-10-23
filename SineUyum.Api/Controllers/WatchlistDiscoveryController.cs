using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using SineUyum.Api.RateLimiting;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // Public lists: discovery, a user's public lists, likes and saves, and the
    // read-only view of a list. A public list is visible to someone when its
    // owner's profile details are (SocialService.VisibleAuthors: no block,
    // Public profile or a Following profile that follows them back).
    [ApiController]
    [Route("api/watchlist")]
    [Authorize]
    public class WatchlistDiscoveryController : ControllerBase
    {
        // Discovery skips nearly empty lists.
        public const int MinDiscoverItems = 3;
        public const int MinCompatibilityCommon = 3;
        public const double ForYouMinCompatibility = 60;
        private const int ForYouCandidates = 300;
        private static readonly TimeSpan TrendingWindow = TimeSpan.FromDays(7);

        private readonly ApplicationDbContext _context;
        private readonly SocialService _social;
        private readonly PrivacyService _privacy;
        private readonly NotificationService _notifications;

        public WatchlistDiscoveryController(ApplicationDbContext context, SocialService social, PrivacyService privacy, NotificationService notifications)
        {
            _context = context;
            _social = social;
            _privacy = privacy;
            _notifications = notifications;
        }

        private string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Others' public lists the viewer may see.
        private IQueryable<Watchlist> PublicLists(string viewerId)
        {
            var visibleOwners = _social.VisibleAuthors(viewerId).Select(u => u.Id);
            return _context.Watchlists.Where(w =>
                w.Visibility == WatchlistVisibility.Public && w.UserId != viewerId && visibleOwners.Contains(w.UserId));
        }

        // Lists the viewer may open read-only: their own and shared lists,
        // visible public lists, and lists sent to or by them in a message.
        // (List ids are sequential, so a private list is no longer readable by
        // just trying ids.)
        private IQueryable<Watchlist> ViewableLists(string viewerId)
        {
            var visibleOwners = _social.VisibleAuthors(viewerId).Select(u => u.Id);
            var blocked = _privacy.BlockedPartnerIds(viewerId);
            return _context.Watchlists.Where(w =>
                w.UserId == viewerId ||
                w.Members.Any(m => m.UserId == viewerId && m.Accepted) ||
                (w.Visibility == WatchlistVisibility.Public && visibleOwners.Contains(w.UserId)) ||
                (!blocked.Contains(w.UserId) &&
                 _context.Messages.Any(m => m.WatchlistId == w.Id && (m.RecipientId == viewerId || m.SenderId == viewerId))));
        }

        // sort: trending (most saved/liked in the last 7 days), popular (all
        // time), new (recently made public), forYou (by owners compatible with
        // the viewer). q searches the name and description.
        [HttpGet("discover")]
        public async Task<ActionResult<WatchlistCardPageDto>> Discover(
            [FromQuery] string? sort = "trending", [FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var lists = PublicLists(Me).Where(w => w.Items.Count >= MinDiscoverItems);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                lists = lists.Where(w => w.Name.ToLower().Contains(term) ||
                                         (w.Description != null && w.Description.ToLower().Contains(term)));
            }

            var since = DateTime.UtcNow - TrendingWindow;
            IQueryable<Watchlist> ordered;
            switch ((sort ?? "trending").ToLowerInvariant())
            {
                case "trending":
                    ordered = lists
                        .OrderByDescending(w => w.Saves.Count(v => v.CreatedAt >= since) * 2 + w.Likes.Count(l => l.CreatedAt >= since))
                        .ThenByDescending(w => w.Saves.Count * 2 + w.Likes.Count)
                        .ThenByDescending(w => w.Id);
                    break;
                case "popular":
                    ordered = lists
                        .OrderByDescending(w => w.Saves.Count * 2 + w.Likes.Count)
                        .ThenByDescending(w => w.Id);
                    break;
                case "new":
                    ordered = lists.OrderByDescending(w => w.PublishedAt).ThenByDescending(w => w.Id);
                    break;
                case "foryou":
                    return Ok(await ForYouAsync(lists, page, pageSize));
                default:
                    throw new DomainException("watchlists.invalidSort");
            }

            var cards = await CardsAsync(ordered.Skip((page - 1) * pageSize).Take(pageSize + 1));
            return Ok(Page(cards, page, pageSize));
        }

        // Popular lists of people with a similar taste, most compatible first.
        private async Task<WatchlistCardPageDto> ForYouAsync(IQueryable<Watchlist> lists, int page, int pageSize)
        {
            var candidates = await lists
                .OrderByDescending(w => w.Saves.Count * 2 + w.Likes.Count).ThenByDescending(w => w.Id)
                .Take(ForYouCandidates)
                .Select(w => new { w.Id, w.UserId, Popularity = w.Saves.Count * 2 + w.Likes.Count })
                .ToListAsync();
            var compatibility = await _social.CompatibilityDetailsAsync(Me, candidates.Select(c => c.UserId).ToList());

            var ids = candidates
                .Where(c => compatibility.TryGetValue(c.UserId, out var x) &&
                            x.CommonCount >= MinCompatibilityCommon && x.Score >= ForYouMinCompatibility)
                .OrderByDescending(c => compatibility[c.UserId].Score).ThenByDescending(c => c.Popularity).ThenByDescending(c => c.Id)
                .Select(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize + 1)
                .ToList();

            var cards = (await CardsAsync(_context.Watchlists.Where(w => ids.Contains(w.Id)))).ToDictionary(c => c.Id);
            return Page(ids.Where(cards.ContainsKey).Select(id => cards[id]).ToList(), page, pageSize);
        }

        // A user's public lists (their profile); same rule as profile details.
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<WatchlistCardPageDto>> ForUser(string userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            await _privacy.EnsureCanViewDetailsAsync(Me, userId, User.IsInRole("Admin"));

            var lists = _context.Watchlists
                .Where(w => w.UserId == userId && w.Visibility == WatchlistVisibility.Public)
                .OrderByDescending(w => w.PublishedAt).ThenByDescending(w => w.Id);
            var cards = await CardsAsync(lists.Skip((page - 1) * pageSize).Take(pageSize + 1));
            return Ok(Page(cards, page, pageSize));
        }

        // Lists I saved that are still public and visible to me, latest saved first.
        [HttpGet("saved")]
        public async Task<ActionResult<WatchlistCardPageDto>> Saved([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var me = Me;
            var lists = PublicLists(me)
                .Where(w => w.Saves.Any(v => v.UserId == me))
                .OrderByDescending(w => w.Saves.Where(v => v.UserId == me).Max(v => v.CreatedAt))
                .ThenByDescending(w => w.Id);
            var cards = await CardsAsync(lists.Skip((page - 1) * pageSize).Take(pageSize + 1));
            return Ok(Page(cards, page, pageSize));
        }

        // Read-only view (lists shared in a message, public lists).
        [HttpGet("public/{listId:int}")]
        public async Task<ActionResult<PublicWatchlistDto>> GetPublic(int listId)
        {
            var me = Me;
            var lang = SupportedLanguages.Current;
            var list = await ViewableLists(me)
                .Where(w => w.Id == listId)
                .Select(w => new PublicWatchlistDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    OwnerUsername = w.User.UserName,
                    Owner = new UserBriefWithImageDto { Id = w.UserId, UserName = w.User.UserName, ProfileImageUrl = w.User.ProfileImageUrl },
                    Visibility = w.Visibility,
                    LikeCount = w.Likes.Count,
                    SaveCount = w.Saves.Count,
                    LikedByMe = w.Likes.Any(l => l.UserId == me),
                    SavedByMe = w.Saves.Any(v => v.UserId == me),
                    CanEdit = w.UserId == me || w.Members.Any(m => m.UserId == me && m.Accepted),
                    Items = w.Items
                        .OrderByDescending(i => i.AddedAt)
                        .Select(i => new WatchlistItemDto
                        {
                            MovieId = i.MovieId,
                            Title = i.Movie.Translations.Where(t => t.Language == lang).Select(t => t.Title).FirstOrDefault() ?? i.Movie.Title,
                            PosterPath = i.Movie.Translations.Where(t => t.Language == lang).Select(t => t.PosterPath).FirstOrDefault() ?? i.Movie.PosterPath,
                            AddedAt = i.AddedAt
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            return list == null ? throw DomainException.NotFound("watchlists.notFound") : Ok(list);
        }

        [HttpPost("{listId:int}/like")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<LikeResultDto>> Like(int listId)
        {
            await LikeableListAsync(listId);
            if (!await _context.WatchlistLikes.AnyAsync(l => l.WatchlistId == listId && l.UserId == Me))
            {
                _context.WatchlistLikes.Add(new WatchlistLike { WatchlistId = listId, UserId = Me });
                await SaveIgnoringDuplicateAsync(() => _context.WatchlistLikes.AnyAsync(l => l.WatchlistId == listId && l.UserId == Me));
            }
            return Ok(new LikeResultDto { Liked = true, LikeCount = await _context.WatchlistLikes.CountAsync(l => l.WatchlistId == listId) });
        }

        [HttpDelete("{listId:int}/like")]
        public async Task<ActionResult<LikeResultDto>> Unlike(int listId)
        {
            await _context.WatchlistLikes.Where(l => l.WatchlistId == listId && l.UserId == Me).ExecuteDeleteAsync();
            return Ok(new LikeResultDto { Liked = false, LikeCount = await _context.WatchlistLikes.CountAsync(l => l.WatchlistId == listId) });
        }

        // Saving notifies the owner (once per person: unsave + save again does
        // not notify twice within a day). Likes do not notify.
        [HttpPost("{listId:int}/save")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<SaveResultDto>> Save(int listId)
        {
            var list = await LikeableListAsync(listId);
            if (!await _context.WatchlistSaves.AnyAsync(v => v.WatchlistId == listId && v.UserId == Me))
            {
                _context.WatchlistSaves.Add(new WatchlistSave { WatchlistId = listId, UserId = Me });
                var url = $"/watchlist/{listId}";
                var notifiedRecently = await _context.Notifications.AnyAsync(n =>
                    n.UserId == list.UserId && n.MessageKey == "notifications.watchlistSaved" && n.RelatedUrl == url &&
                    n.MessageArgs != null && n.MessageArgs.Contains(Me) && n.CreatedAt > DateTime.UtcNow.AddDays(-1));
                if (notifiedRecently)
                {
                    await SaveIgnoringDuplicateAsync(() => _context.WatchlistSaves.AnyAsync(v => v.WatchlistId == listId && v.UserId == Me));
                }
                else
                {
                    var myName = await _context.Users.Where(u => u.Id == Me).Select(u => u.UserName).SingleAsync() ?? string.Empty;
                    try
                    {
                        // The save and the notification in one SaveChanges.
                        await _notifications.NotifyAsync(list.UserId, url, "notifications.watchlistSaved", myName, list.Name, Me);
                    }
                    catch (DbUpdateException)
                    {
                        // A concurrent request saved it already: same result, no second notification.
                        _context.ChangeTracker.Clear();
                        if (!await _context.WatchlistSaves.AnyAsync(v => v.WatchlistId == listId && v.UserId == Me)) throw;
                    }
                }
            }
            return Ok(new SaveResultDto { Saved = true, SaveCount = await _context.WatchlistSaves.CountAsync(v => v.WatchlistId == listId) });
        }

        [HttpDelete("{listId:int}/save")]
        public async Task<ActionResult<SaveResultDto>> Unsave(int listId)
        {
            await _context.WatchlistSaves.Where(v => v.WatchlistId == listId && v.UserId == Me).ExecuteDeleteAsync();
            return Ok(new SaveResultDto { Saved = false, SaveCount = await _context.WatchlistSaves.CountAsync(v => v.WatchlistId == listId) });
        }

        // Only others' public lists that the viewer may see.
        private async Task<Watchlist> LikeableListAsync(int listId)
        {
            var list = await _context.Watchlists.FirstOrDefaultAsync(w => w.Id == listId);
            if (list != null && list.UserId == Me) throw new DomainException("watchlists.ownList");
            return await PublicLists(Me).FirstOrDefaultAsync(w => w.Id == listId)
                ?? throw DomainException.NotFound("watchlists.notFound");
        }

        private async Task SaveIgnoringDuplicateAsync(Func<Task<bool>> exists)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request did the same: same result; otherwise a real error.
                _context.ChangeTracker.Clear();
                if (!await exists()) throw;
            }
        }

        // Cards for an already filtered, ordered and paged query.
        private async Task<List<WatchlistCardDto>> CardsAsync(IQueryable<Watchlist> lists)
        {
            var me = Me;
            var lang = SupportedLanguages.Current;
            var rows = await lists
                .Select(w => new
                {
                    Card = new WatchlistCardDto
                    {
                        Id = w.Id,
                        Name = w.Name,
                        Description = w.Description,
                        Owner = new UserBriefWithImageDto { Id = w.UserId, UserName = w.User.UserName, ProfileImageUrl = w.User.ProfileImageUrl },
                        ItemCount = w.Items.Count,
                        LikeCount = w.Likes.Count,
                        SaveCount = w.Saves.Count,
                        LikedByMe = w.Likes.Any(l => l.UserId == me),
                        SavedByMe = w.Saves.Any(v => v.UserId == me),
                        PublishedAt = w.PublishedAt
                    },
                    LatestMovieIds = w.Items
                        .OrderByDescending(i => i.AddedAt).ThenByDescending(i => i.Id)
                        .Take(4)
                        .Select(i => i.MovieId)
                        .ToList()
                })
                .ToListAsync();

            // Posters in the request language, in a second query (a nested
            // per-language lookup inside the per-list Take needs SQL APPLY,
            // which SQLite does not have).
            var movieIds = rows.SelectMany(r => r.LatestMovieIds).Distinct().ToList();
            var posters = await _context.Movies
                .Where(m => movieIds.Contains(m.Id))
                .Select(m => new
                {
                    m.Id,
                    PosterPath = m.Translations.Where(t => t.Language == lang).Select(t => t.PosterPath).FirstOrDefault() ?? m.PosterPath
                })
                .ToDictionaryAsync(m => m.Id, m => m.PosterPath);

            var compatibility = await _social.CompatibilityDetailsAsync(me, rows.Select(r => r.Card.Owner.Id).ToList());
            foreach (var row in rows)
            {
                row.Card.Posters = row.LatestMovieIds.Select(id => posters.GetValueOrDefault(id)).ToList();
                if (compatibility.TryGetValue(row.Card.Owner.Id, out var c) && c.CommonCount >= MinCompatibilityCommon)
                {
                    row.Card.OwnerCompatibility = c.Score;
                }
            }
            return rows.Select(r => r.Card).ToList();
        }

        private static WatchlistCardPageDto Page(List<WatchlistCardDto> cards, int page, int pageSize) => new()
        {
            Items = cards.Take(pageSize).ToList(),
            Page = page,
            HasMore = cards.Count > pageSize
        };
    }
}
