using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
using SineUyum.Api.Services;

namespace SineUyum.Api.Controllers
{
    // Ratings and reviews of followed users (and one's own), newest first.
    // Cursor pagination: pages do not shift as new activity arrives.
    [ApiController]
    [Route("api/feed")]
    [Authorize]
    public class FeedController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SocialService _social;

        public FeedController(ApplicationDbContext context, SocialService social)
        {
            _context = context;
            _social = social;
        }

        [HttpGet]
        public async Task<ActionResult<FeedPageDto>> Get([FromQuery] string? cursor, [FromQuery] int limit = 20)
        {
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            limit = Math.Clamp(limit, 1, 50);

            var following = _context.UserFollows.Where(f => f.FollowerId == me).Select(f => f.FollowingId);
            var visible = _social.VisibleAuthors(me).Select(u => u.Id);
            var query = _context.UserRatings.Where(r =>
                r.ActivityAt != null &&
                (r.UserId == me || following.Contains(r.UserId)) &&
                visible.Contains(r.UserId));

            if (!string.IsNullOrEmpty(cursor))
            {
                var (at, id) = ParseCursor(cursor);
                query = query.Where(r => r.ActivityAt < at || (r.ActivityAt == at && r.Id < id));
            }

            var lang = SupportedLanguages.Current;
            var rows = await query
                .OrderByDescending(r => r.ActivityAt).ThenByDescending(r => r.Id)
                .Take(limit + 1)
                .Select(r => new
                {
                    r.Id,
                    ActivityAt = r.ActivityAt!.Value,
                    r.UserId,
                    r.User.UserName,
                    r.User.ProfileImageUrl,
                    r.MovieId,
                    MovieTitle = r.Movie.Translations.Where(t => t.Language == lang).Select(t => t.Title).FirstOrDefault() ?? r.Movie.Title,
                    MoviePosterPath = r.Movie.PosterPath,
                    r.Rating,
                    ReviewId = _context.Reviews.Where(v => v.UserId == r.UserId && v.MovieId == r.MovieId).Select(v => (int?)v.Id).FirstOrDefault()
                })
                .ToListAsync();

            var hasMore = rows.Count > limit;
            var page = rows.Take(limit).ToList();

            var reviewIds = page.Where(r => r.ReviewId != null).Select(r => r.ReviewId!.Value).ToList();
            var reviews = reviewIds.Count == 0
                ? new Dictionary<int, ReviewDto>()
                : (await _social.ToDtosAsync(me, _context.Reviews.Where(v => reviewIds.Contains(v.Id)))).ToDictionary(v => v.Id);
            var compatibility = await _social.CompatibilityWithAsync(me, page.Select(r => r.UserId).ToList());

            var items = page.Select(r => new FeedItemDto
            {
                Type = r.ReviewId != null ? "Reviewed" : "Rated",
                ActivityAt = r.ActivityAt,
                Author = new UserBriefWithImageDto { Id = r.UserId, UserName = r.UserName, ProfileImageUrl = r.ProfileImageUrl },
                Movie = new MovieSummaryDto { Id = r.MovieId, Title = r.MovieTitle, PosterPath = r.MoviePosterPath },
                Rating = r.Rating,
                Review = r.ReviewId != null && reviews.TryGetValue(r.ReviewId.Value, out var review) ? review : null,
                AuthorCompatibility = compatibility.TryGetValue(r.UserId, out var score) ? score : null
            }).ToList();

            var last = page.LastOrDefault();
            return Ok(new FeedPageDto
            {
                Items = items,
                NextCursor = hasMore && last != null
                    ? $"{last.ActivityAt.Ticks.ToString(CultureInfo.InvariantCulture)}_{last.Id.ToString(CultureInfo.InvariantCulture)}"
                    : null
            });
        }

        private static (DateTime At, int Id) ParseCursor(string cursor)
        {
            var parts = cursor.Split('_');
            if (parts.Length == 2 &&
                long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) &&
                ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks &&
                int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                return (new DateTime(ticks, DateTimeKind.Utc), id);
            }
            throw new DomainException("feed.invalidCursor");
        }
    }
}
