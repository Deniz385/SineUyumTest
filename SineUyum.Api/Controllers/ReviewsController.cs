using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
    // Reviews, likes and comments. Visibility follows the same rule as profile
    // details (SocialService.VisibleAuthors): a review by a private or blocked
    // author is treated as if it did not exist (404).
    [ApiController]
    [Route("api/reviews")]
    [Authorize]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SocialService _social;
        private readonly PrivacyService _privacy;
        private readonly NotificationService _notifications;
        private readonly IStringLocalizer _t;

        public ReviewsController(ApplicationDbContext context, SocialService social, PrivacyService privacy, NotificationService notifications, IStringLocalizer localizer)
        {
            _context = context;
            _social = social;
            _privacy = privacy;
            _notifications = notifications;
            _t = localizer;
        }

        private string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet("movie/{movieId:int}")]
        public async Task<ActionResult<MovieReviewsDto>> ForMovie(int movieId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var visible = _social.VisibleAuthors(Me).Select(u => u.Id);
            var others = _context.Reviews.Where(r => r.MovieId == movieId && r.UserId != Me && visible.Contains(r.UserId));

            var mine = await _social.ToDtosAsync(Me, _context.Reviews.Where(r => r.MovieId == movieId && r.UserId == Me));
            var reviews = await _social.ToDtosAsync(Me, others
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize));

            return Ok(new MovieReviewsDto
            {
                MyRating = await _context.UserRatings
                    .Where(r => r.UserId == Me && r.MovieId == movieId)
                    .Select(r => (int?)r.Rating)
                    .FirstOrDefaultAsync(),
                Mine = mine.SingleOrDefault(),
                Reviews = reviews,
                TotalCount = await others.CountAsync(),
                // Only on the first page (it does not change between pages).
                ForYou = page == 1 ? await _social.ForYouScoreAsync(Me, movieId) : null
            });
        }

        // A user's reviews (profile), newest first; the same rule as profile details.
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ReviewPageDto>> ForUser(string userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            await _privacy.EnsureCanViewDetailsAsync(Me, userId, User.IsInRole("Admin"));

            var reviews = await _social.ToDtosAsync(Me, _context.Reviews
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize + 1));
            return Ok(new ReviewPageDto { Items = reviews.Take(pageSize).ToList(), Page = page, HasMore = reviews.Count > pageSize });
        }

        // Writes or updates one's own review. A review is the written form of
        // a rating: the rating comes first.
        [HttpPut("movie/{movieId:int}")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<ReviewDto>> Upsert(int movieId, [FromBody] UpsertReviewDto dto)
        {
            var rating = await _context.UserRatings.FirstOrDefaultAsync(r => r.UserId == Me && r.MovieId == movieId);
            if (rating == null) throw new DomainException("reviews.ratingRequired");

            var text = dto.Text.Trim();
            if (text.Length == 0) throw new DomainException("reviews.textRequired");

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.UserId == Me && r.MovieId == movieId);
            if (review == null)
            {
                review = new Review { UserId = Me, MovieId = movieId, Text = text, ContainsSpoilers = dto.ContainsSpoilers };
                _context.Reviews.Add(review);
                // The first review moves the activity up in the feed; edits do not.
                rating.ActivityAt = review.CreatedAt;
            }
            else
            {
                review.Text = text;
                review.ContainsSpoilers = dto.ContainsSpoilers;
                review.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            return Ok((await _social.ToDtosAsync(Me, _context.Reviews.Where(r => r.Id == review.Id))).Single());
        }

        [HttpDelete("movie/{movieId:int}")]
        public async Task<ActionResult<StatusMessageDto>> Delete(int movieId)
        {
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.UserId == Me && r.MovieId == movieId);
            if (review != null)
            {
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
            }
            return Ok(new StatusMessageDto(_t["reviews.deleted"]));
        }

        [HttpPost("{reviewId:int}/like")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<LikeResultDto>> Like(int reviewId)
        {
            var review = await VisibleReviewAsync(reviewId);
            if (!await _context.ReviewLikes.AnyAsync(l => l.ReviewId == reviewId && l.UserId == Me))
            {
                _context.ReviewLikes.Add(new ReviewLike { ReviewId = reviewId, UserId = Me });
                try
                {
                    if (review.UserId != Me)
                    {
                        // The like and its notification are saved together.
                        await _notifications.NotifyAsync(review.UserId,
                            $"/movie/{review.MovieId}",
                            "notifications.reviewLiked", await MyNameAsync(), new MovieRef(review.MovieId, review.Movie.Title));
                    }
                    else
                    {
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateException)
                {
                    // A concurrent second request already liked it: same outcome. Otherwise a real error.
                    if (!await LikeExistsAsync(reviewId)) throw;
                }
            }
            return Ok(new LikeResultDto { Liked = true, LikeCount = await _context.ReviewLikes.CountAsync(l => l.ReviewId == reviewId) });
        }

        [HttpDelete("{reviewId:int}/like")]
        public async Task<ActionResult<LikeResultDto>> Unlike(int reviewId)
        {
            await VisibleReviewAsync(reviewId);
            await _context.ReviewLikes.Where(l => l.ReviewId == reviewId && l.UserId == Me).ExecuteDeleteAsync();
            return Ok(new LikeResultDto { Liked = false, LikeCount = await _context.ReviewLikes.CountAsync(l => l.ReviewId == reviewId) });
        }

        [HttpGet("{reviewId:int}/comments")]
        public async Task<ActionResult<List<ReviewCommentDto>>> Comments(int reviewId)
        {
            var review = await VisibleReviewAsync(reviewId);
            var blocked = _privacy.BlockedPartnerIds(Me);
            var comments = await _context.ReviewComments
                .Where(c => c.ReviewId == reviewId && !blocked.Contains(c.UserId))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                .Take(200)
                .Select(c => new ReviewCommentDto
                {
                    Id = c.Id,
                    Author = new UserBriefWithImageDto { Id = c.UserId, UserName = c.User.UserName, ProfileImageUrl = c.User.ProfileImageUrl },
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    CanDelete = c.UserId == Me || review.UserId == Me
                })
                .ToListAsync();
            return Ok(comments);
        }

        [HttpPost("{reviewId:int}/comments")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<ReviewCommentDto>> AddComment(int reviewId, [FromBody] CreateCommentDto dto)
        {
            var review = await VisibleReviewAsync(reviewId);
            var text = dto.Text.Trim();
            if (text.Length == 0) throw new DomainException("reviews.commentRequired");

            var comment = new ReviewComment { ReviewId = reviewId, UserId = Me, Text = text };
            _context.ReviewComments.Add(comment);
            if (review.UserId != Me)
            {
                // The notification has no comment text (we keep content out of push bodies).
                await _notifications.NotifyAsync(review.UserId,
                    $"/movie/{review.MovieId}",
                    "notifications.reviewCommented", await MyNameAsync(), new MovieRef(review.MovieId, review.Movie.Title));
            }
            else
            {
                await _context.SaveChangesAsync();
            }

            var me = await _context.Users.Where(u => u.Id == Me).Select(u => new { u.UserName, u.ProfileImageUrl }).SingleAsync();
            return Ok(new ReviewCommentDto
            {
                Id = comment.Id,
                Author = new UserBriefWithImageDto { Id = Me, UserName = me.UserName, ProfileImageUrl = me.ProfileImageUrl },
                Text = comment.Text,
                CreatedAt = comment.CreatedAt,
                CanDelete = true
            });
        }

        [HttpDelete("comments/{commentId:int}")]
        public async Task<ActionResult<StatusMessageDto>> DeleteComment(int commentId)
        {
            var comment = await _context.ReviewComments.Include(c => c.Review).FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null) throw DomainException.NotFound("reviews.commentNotFound");
            if (comment.UserId != Me && comment.Review.UserId != Me)
            {
                throw new DomainException("reviews.cannotDeleteComment", StatusCodes.Status403Forbidden);
            }
            _context.ReviewComments.Remove(comment);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto(_t["reviews.commentDeleted"]));
        }

        private async Task<Review> VisibleReviewAsync(int reviewId)
        {
            var visible = _social.VisibleAuthors(Me).Select(u => u.Id);
            var review = await _context.Reviews.Include(r => r.Movie)
                .FirstOrDefaultAsync(r => r.Id == reviewId && visible.Contains(r.UserId));
            return review ?? throw DomainException.NotFound("reviews.notFound");
        }

        private async Task<string> MyNameAsync() =>
            await _context.Users.Where(u => u.Id == Me).Select(u => u.UserName).SingleAsync() ?? _t["common.someone"];

        private async Task<bool> LikeExistsAsync(int reviewId)
        {
            _context.ChangeTracker.Clear();
            return await _context.ReviewLikes.AnyAsync(l => l.ReviewId == reviewId && l.UserId == Me);
        }
    }
}
