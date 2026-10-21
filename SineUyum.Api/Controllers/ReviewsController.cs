using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.RateLimiting;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // İncelemeler, beğeniler ve yorumlar. Görünürlük profil ayrıntılarıyla
    // aynı kuraldır (SocialService.VisibleAuthors): gizli/engelli yazarın
    // incelemesi yokmuş gibi davranılır (404).
    [ApiController]
    [Route("api/reviews")]
    [Authorize]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SocialService _social;
        private readonly PrivacyService _privacy;
        private readonly NotificationService _notifications;

        public ReviewsController(ApplicationDbContext context, SocialService social, PrivacyService privacy, NotificationService notifications)
        {
            _context = context;
            _social = social;
            _privacy = privacy;
            _notifications = notifications;
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
                // Yalnızca ilk sayfada (sayfalar arasında değişmez).
                ForYou = page == 1 ? await _social.ForYouScoreAsync(Me, movieId) : null
            });
        }

        // Kendi incelemesini yazar ya da günceller. İnceleme bir puanın yazılı
        // hâlidir: önce puan gerekir.
        [HttpPut("movie/{movieId:int}")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<ReviewDto>> Upsert(int movieId, [FromBody] UpsertReviewDto dto)
        {
            var rating = await _context.UserRatings.FirstOrDefaultAsync(r => r.UserId == Me && r.MovieId == movieId);
            if (rating == null) throw new DomainException("İnceleme yazmadan önce filmi puanla.");

            var text = dto.Text.Trim();
            if (text.Length == 0) throw new DomainException("İnceleme metni boş olamaz.");

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.UserId == Me && r.MovieId == movieId);
            if (review == null)
            {
                review = new Review { UserId = Me, MovieId = movieId, Text = text, ContainsSpoilers = dto.ContainsSpoilers };
                _context.Reviews.Add(review);
                // İlk inceleme akışta öne çıkar; düzenleme çıkarmaz.
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
            return Ok(new StatusMessageDto("İnceleme silindi."));
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
                        // Beğeni ve bildirimi tek kayıtta.
                        await _notifications.NotifyAsync(review.UserId,
                            $"{await MyNameAsync()} '{review.Movie.Title}' incelemeni beğendi.",
                            $"/movie/{review.MovieId}");
                    }
                    else
                    {
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateException)
                {
                    // Eşzamanlı ikinci istek zaten beğenmişse sonuç aynı; değilse gerçek hata.
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
            if (text.Length == 0) throw new DomainException("Yorum boş olamaz.");

            var comment = new ReviewComment { ReviewId = reviewId, UserId = Me, Text = text };
            _context.ReviewComments.Add(comment);
            if (review.UserId != Me)
            {
                // Bildirimde yorum metni yok (push gövdesine içerik koymuyoruz).
                await _notifications.NotifyAsync(review.UserId,
                    $"{await MyNameAsync()} '{review.Movie.Title}' incelemene yorum yaptı.",
                    $"/movie/{review.MovieId}");
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
            if (comment == null) throw DomainException.NotFound("Yorum bulunamadı.");
            if (comment.UserId != Me && comment.Review.UserId != Me)
            {
                throw new DomainException("Bu yorumu yalnızca yazarı ya da inceleme sahibi silebilir.", StatusCodes.Status403Forbidden);
            }
            _context.ReviewComments.Remove(comment);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto("Yorum silindi."));
        }

        private async Task<Review> VisibleReviewAsync(int reviewId)
        {
            var visible = _social.VisibleAuthors(Me).Select(u => u.Id);
            var review = await _context.Reviews.Include(r => r.Movie)
                .FirstOrDefaultAsync(r => r.Id == reviewId && visible.Contains(r.UserId));
            return review ?? throw DomainException.NotFound("İnceleme bulunamadı.");
        }

        private async Task<string> MyNameAsync() =>
            await _context.Users.Where(u => u.Id == Me).Select(u => u.UserName).SingleAsync() ?? "Biri";

        private async Task<bool> LikeExistsAsync(int reviewId)
        {
            _context.ChangeTracker.Clear();
            return await _context.ReviewLikes.AnyAsync(l => l.ReviewId == reviewId && l.UserId == Me);
        }
    }
}
