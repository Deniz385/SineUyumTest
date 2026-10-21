using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;

namespace SineUyum.Api.Services
{
    // İnceleme ve akışın ortak kuralları: kimin içeriği kime görünür (G12
    // ile aynı kural) ve yazar başına uyum rozeti.
    public class SocialService
    {
        private readonly ApplicationDbContext _context;
        private readonly PrivacyService _privacy;

        public SocialService(ApplicationDbContext context, PrivacyService privacy)
        {
            _context = context;
            _privacy = privacy;
        }

        // Puan ve incelemeler profil ayrıntısıdır: engel yoksa ve yazarın
        // görünürlüğü izin veriyorsa (Herkes / yazarın takip ettiği biriyse)
        // görünür. Kendi içeriği her zaman görünür.
        public IQueryable<AppUser> VisibleAuthors(string viewerId)
        {
            var blocked = _privacy.BlockedPartnerIds(viewerId);
            var followsViewer = _context.UserFollows.Where(f => f.FollowingId == viewerId).Select(f => f.FollowerId);
            return _context.Users.Where(u =>
                u.Id == viewerId ||
                (!blocked.Contains(u.Id) &&
                 (u.ProfileVisibility == ProfileVisibility.Public ||
                  (u.ProfileVisibility == ProfileVisibility.Following && followsViewer.Contains(u.Id)))));
        }

        public async Task<bool> CanSeeAuthorAsync(string viewerId, string authorId) =>
            await VisibleAuthors(viewerId).AnyAsync(u => u.Id == authorId);

        // Görüntüleyenin verilen yazarlarla uyumu (tek sorgu). Ortak filmi
        // olmayanlar ve kendisi listede yer almaz.
        public async Task<Dictionary<string, double>> CompatibilityWithAsync(string viewerId, IReadOnlyCollection<string> authorIds)
        {
            var others = authorIds.Where(id => id != viewerId).Distinct().ToList();
            if (others.Count == 0) return new();

            var pairs = await (
                from mine in _context.UserRatings.Where(r => r.UserId == viewerId)
                join theirs in _context.UserRatings.Where(r => others.Contains(r.UserId)) on mine.MovieId equals theirs.MovieId
                select new { theirs.UserId, Mine = mine.Rating, Theirs = theirs.Rating })
                .ToListAsync();

            return pairs
                .GroupBy(p => p.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => Math.Round(CompatibilityCalculator.Score(g.Select(p => (p.Mine, p.Theirs)).ToList()), 0));
        }

        // İncelemeleri görüntüleyen için DTO'ya çevirir (beğeni/yorum sayıları,
        // beğenmiş mi, yazarın puanı ve uyum rozeti).
        public async Task<List<ReviewDto>> ToDtosAsync(string viewerId, IQueryable<Review> reviews)
        {
            var rows = await reviews
                .Select(r => new
                {
                    r.Id,
                    r.MovieId,
                    MovieTitle = r.Movie.Title,
                    MoviePosterPath = r.Movie.PosterPath,
                    r.UserId,
                    r.User.UserName,
                    r.User.ProfileImageUrl,
                    Rating = _context.UserRatings
                        .Where(ur => ur.UserId == r.UserId && ur.MovieId == r.MovieId)
                        .Select(ur => (int?)ur.Rating)
                        .FirstOrDefault(),
                    r.Text,
                    r.ContainsSpoilers,
                    r.CreatedAt,
                    r.UpdatedAt,
                    LikeCount = r.Likes.Count,
                    CommentCount = r.Comments.Count,
                    LikedByMe = r.Likes.Any(l => l.UserId == viewerId)
                })
                .ToListAsync();

            var compatibility = await CompatibilityWithAsync(viewerId, rows.Select(r => r.UserId).ToList());
            return rows.Select(r => new ReviewDto
            {
                Id = r.Id,
                Movie = new MovieSummaryDto { Id = r.MovieId, Title = r.MovieTitle, PosterPath = r.MoviePosterPath },
                Author = new UserBriefWithImageDto { Id = r.UserId, UserName = r.UserName, ProfileImageUrl = r.ProfileImageUrl },
                Rating = r.Rating,
                Text = r.Text,
                ContainsSpoilers = r.ContainsSpoilers,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                LikeCount = r.LikeCount,
                CommentCount = r.CommentCount,
                LikedByMe = r.LikedByMe,
                IsMine = r.UserId == viewerId,
                AuthorCompatibility = compatibility.TryGetValue(r.UserId, out var score) ? score : null
            }).ToList();
        }
    }
}
