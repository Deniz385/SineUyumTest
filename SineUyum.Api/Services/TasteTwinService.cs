using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;

namespace SineUyum.Api.Services
{
    // Weekly taste twin: the most compatible person you do not follow yet and
    // who was never suggested before. Only people open to discovery qualify: a
    // public profile, not hidden from search, and no block between you.
    public class TasteTwinService
    {
        public const int MinRatings = 5;
        public const int MinCommonMovies = 5;
        public const double MinCompatibility = 70;
        private const int MaxCandidates = 200;

        private readonly ApplicationDbContext _context;
        private readonly SocialService _social;
        private readonly PrivacyService _privacy;
        private readonly NotificationService _notifications;

        public TasteTwinService(ApplicationDbContext context, SocialService social, PrivacyService privacy, NotificationService notifications)
        {
            _context = context;
            _social = social;
            _privacy = privacy;
            _notifications = notifications;
        }

        public static DateOnly WeekStartOf(DateTime utc)
        {
            var date = DateOnly.FromDateTime(utc);
            var sinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-sinceMonday);
        }

        // This week's suggestion; computed and saved if missing. Null if no candidate fits.
        public async Task<TasteTwinSuggestion?> ForThisWeekAsync(string userId, DateTime nowUtc)
        {
            var week = WeekStartOf(nowUtc);
            var existing = await _context.TasteTwinSuggestions.FirstOrDefaultAsync(t => t.UserId == userId && t.WeekStart == week);
            if (existing != null) return existing;

            var pick = await FindAsync(userId);
            if (pick == null) return null;

            var suggestion = new TasteTwinSuggestion
            {
                UserId = userId,
                SuggestedUserId = pick.Value.UserId,
                WeekStart = week,
                Compatibility = pick.Value.Score,
                CommonMovieCount = pick.Value.CommonCount
            };
            _context.TasteTwinSuggestions.Add(suggestion);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request already wrote this week's suggestion.
                _context.ChangeTracker.Clear();
                return await _context.TasteTwinSuggestions.FirstOrDefaultAsync(t => t.UserId == userId && t.WeekStart == week);
            }
            return suggestion;
        }

        // Sends the suggestion as a notification (weekly job). Only once.
        public async Task NotifyAsync(TasteTwinSuggestion suggestion)
        {
            if (suggestion.Notified) return;
            var name = await _context.Users.Where(u => u.Id == suggestion.SuggestedUserId).Select(u => u.UserName).SingleAsync();
            suggestion.Notified = true;
            await _notifications.NotifyAsync(suggestion.UserId,
                $"/profile/{suggestion.SuggestedUserId}",
                "notifications.tasteTwin", name, suggestion.Compatibility, suggestion.CommonMovieCount);
        }

        public async Task<TasteTwinDto?> ToDtoAsync(TasteTwinSuggestion? suggestion, string viewerId)
        {
            if (suggestion == null) return null;
            // The person may have been blocked or hidden since the suggestion.
            if (!await _social.CanSeeAuthorAsync(viewerId, suggestion.SuggestedUserId)) return null;

            var user = await _context.Users
                .Where(u => u.Id == suggestion.SuggestedUserId)
                .Select(u => new { u.UserName, u.ProfileImageUrl, u.Bio })
                .SingleAsync();
            return new TasteTwinDto
            {
                User = new UserBriefWithImageDto { Id = suggestion.SuggestedUserId, UserName = user.UserName, ProfileImageUrl = user.ProfileImageUrl },
                Bio = user.Bio,
                Compatibility = suggestion.Compatibility,
                CommonMovieCount = suggestion.CommonMovieCount,
                WeekStart = suggestion.WeekStart,
                IsFollowing = await _context.UserFollows.AnyAsync(f => f.FollowerId == viewerId && f.FollowingId == suggestion.SuggestedUserId)
            };
        }

        private async Task<(string UserId, double Score, int CommonCount)?> FindAsync(string userId)
        {
            var myMovies = _context.UserRatings.Where(r => r.UserId == userId).Select(r => r.MovieId);
            if (await myMovies.CountAsync() < MinRatings) return null;

            var blocked = _privacy.BlockedPartnerIds(userId);
            var following = _context.UserFollows.Where(f => f.FollowerId == userId).Select(f => f.FollowingId);
            var suggestedBefore = _context.TasteTwinSuggestions.Where(t => t.UserId == userId).Select(t => t.SuggestedUserId);

            // Candidates with the most common movies (compatibility is computed among them).
            var candidates = await _context.UserRatings
                .Where(r => myMovies.Contains(r.MovieId) && r.UserId != userId)
                .Where(r => r.User.ProfileVisibility == ProfileVisibility.Public && !r.User.HiddenFromSearch)
                .Where(r => !blocked.Contains(r.UserId) && !following.Contains(r.UserId) && !suggestedBefore.Contains(r.UserId))
                .GroupBy(r => r.UserId)
                .Where(g => g.Count() >= MinCommonMovies)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(MaxCandidates)
                .ToListAsync();
            if (candidates.Count == 0) return null;

            var scores = await _social.CompatibilityDetailsAsync(userId, candidates);
            var best = scores
                .Where(s => s.Value.CommonCount >= MinCommonMovies && s.Value.Score >= MinCompatibility)
                .OrderByDescending(s => s.Value.Score)
                .ThenByDescending(s => s.Value.CommonCount)
                .ThenBy(s => s.Key, StringComparer.Ordinal)
                .Select(s => ((string, double, int)?)(s.Key, s.Value.Score, s.Value.CommonCount))
                .FirstOrDefault();
            return best;
        }
    }
}
