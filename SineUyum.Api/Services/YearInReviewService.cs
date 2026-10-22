using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Localization;

namespace SineUyum.Api.Services
{
    // A user's year in movies. The year's movies are those logged in the diary
    // that year (by watch date) plus those first rated that year; their current
    // ratings make up the year's taste profile (FilmIdentity on that set).
    // Ratings from before the feed existed have no date and only count through
    // the diary.
    public class YearInReviewService
    {
        public const int MinMovies = 3;
        public const int MinWeekdayEntries = 3;
        public const int MinTasteMateCommon = 5;
        private const int MaxTasteMateCandidates = 500;

        private readonly ApplicationDbContext _context;
        private readonly SocialService _social;

        public YearInReviewService(ApplicationDbContext context, SocialService social)
        {
            _context = context;
            _social = social;
        }

        private sealed record DiaryRow(int Id, int MovieId, DateOnly WatchedOn, bool Rewatch);

        private sealed record MovieRow(int Id, string Title, string? PosterPath, double? TmdbVoteAverage, List<(int Id, string Name)> Genres);

        public async Task<YearInReviewDto> BuildAsync(string viewerId, string ownerId, string ownerName, int year, IStringLocalizer t)
        {
            var lang = SupportedLanguages.Current;
            var (from, to) = (new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var diary = await DiaryAsync(ownerId, year);
            var ratedThisYear = await _context.UserRatings
                .Where(r => r.UserId == ownerId && r.CreatedAt >= from && r.CreatedAt < to)
                .Select(r => new { r.MovieId, CreatedAt = r.CreatedAt!.Value })
                .ToListAsync();

            var movieIds = diary.Select(d => d.MovieId).Concat(ratedThisYear.Select(r => r.MovieId)).Distinct().ToList();
            var ratings = await _context.UserRatings
                .Where(r => r.UserId == ownerId && movieIds.Contains(r.MovieId))
                .ToDictionaryAsync(r => r.MovieId, r => r.Rating);

            var movies = (await _context.Movies
                .Where(m => movieIds.Contains(m.Id))
                .Select(m => new
                {
                    m.Id,
                    Title = m.Translations.Where(x => x.Language == lang).Select(x => x.Title).FirstOrDefault() ?? m.Title,
                    PosterPath = m.Translations.Where(x => x.Language == lang).Select(x => x.PosterPath).FirstOrDefault() ?? m.PosterPath,
                    m.TmdbVoteAverage,
                    Genres = m.MovieGenres.Select(mg => new { mg.Genre.Id, mg.Genre.Name }).ToList()
                })
                .ToListAsync())
                .ToDictionary(m => m.Id, m => new MovieRow(m.Id, m.Title, m.PosterPath, m.TmdbVoteAverage,
                    m.Genres.Select(g => (g.Id, g.Name)).ToList()));

            var facts = movieIds
                .Where(id => ratings.ContainsKey(id) && movies.ContainsKey(id))
                .Select(id => movies[id])
                .Select(m => new RatedMovieFacts(m.Id, m.Title, m.PosterPath, ratings[m.Id], m.TmdbVoteAverage,
                    m.Genres.Select(g => t.GenreName(g.Id, g.Name)).ToList()))
                .ToList();
            var identity = FilmIdentity.Build(ownerName, facts, t);

            // Activity per month: every diary entry, plus the rating date of
            // movies that were not logged that year (no double counting).
            var logged = diary.Select(d => d.MovieId).ToHashSet();
            var activity = diary.Select(d => d.WatchedOn.Month)
                .Concat(ratedThisYear.Where(r => !logged.Contains(r.MovieId)).Select(r => r.CreatedAt.Month))
                .ToList();
            var months = Enumerable.Range(1, 12)
                .Select(m => new MonthCountDto { Month = m, Count = activity.Count(a => a == m) })
                .ToList();
            var busiest = months.Where(m => m.Count > 0).OrderByDescending(m => m.Count).ThenBy(m => m.Month).FirstOrDefault();

            var weekday = diary.Count < MinWeekdayEntries ? null : diary
                .GroupBy(d => IsoWeekday(d.WatchedOn))
                .Select(g => new WeekdayCountDto { Weekday = g.Key, Count = g.Count() })
                .OrderByDescending(w => w.Count).ThenBy(w => w.Weekday)
                .First();

            var ordered = diary.OrderBy(d => d.WatchedOn).ThenBy(d => d.Id).ToList();
            var rewatched = diary
                .GroupBy(d => d.MovieId)
                .Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Max(d => d.WatchedOn)).ThenBy(g => g.Key)
                .FirstOrDefault();

            var previous = await PreviousYearAverageAsync(ownerId, year - 1);

            return new YearInReviewDto
            {
                Year = year,
                UserName = ownerName,
                IsMine = viewerId == ownerId,
                Ready = movieIds.Count >= MinMovies,
                MinMovies = MinMovies,
                AvailableYears = await AvailableYearsAsync(ownerId),
                Totals = new YearTotalsDto
                {
                    Movies = movieIds.Count,
                    DiaryEntries = diary.Count,
                    Rewatches = diary.Count(d => d.Rewatch),
                    Ratings = ratedThisYear.Count,
                    Reviews = await _context.Reviews.CountAsync(r => r.UserId == ownerId && r.CreatedAt >= from && r.CreatedAt < to),
                    LikesReceived = await _context.ReviewLikes.CountAsync(l =>
                        l.Review.UserId == ownerId && l.UserId != ownerId && l.CreatedAt >= from && l.CreatedAt < to),
                    CommentsReceived = await _context.ReviewComments.CountAsync(c =>
                        c.Review.UserId == ownerId && c.UserId != ownerId && c.CreatedAt >= from && c.CreatedAt < to),
                },
                Months = months,
                BusiestMonth = busiest,
                FavoriteWeekday = weekday,
                AverageRating = facts.Count == 0 ? null : identity.AverageRating,
                PreviousYearAverage = previous,
                Identity = identity,
                TopMovies = facts
                    .OrderByDescending(f => f.Rating).ThenBy(f => f.MovieId)
                    .Take(5)
                    .Select(f => new IdentityMovieDto
                    {
                        MovieId = f.MovieId, Title = f.Title, PosterPath = f.PosterPath, Rating = f.Rating, TmdbAverage = f.TmdbAverage
                    })
                    .ToList(),
                FirstMovie = ordered.Count == 0 ? null : Dated(ordered[0], movies),
                LastMovie = ordered.Count < 2 ? null : Dated(ordered[^1], movies),
                MostRewatched = rewatched == null || !movies.TryGetValue(rewatched.Key, out var r) ? null : new RewatchedMovieDto
                {
                    Movie = Summary(r),
                    Count = rewatched.Count()
                },
                TasteMate = await TasteMateAsync(viewerId, ownerId),
            };
        }

        private Task<List<DiaryRow>> DiaryAsync(string userId, int year)
        {
            var (from, to) = (new DateOnly(year, 1, 1), new DateOnly(year + 1, 1, 1));
            return _context.DiaryEntries
                .Where(d => d.UserId == userId && d.WatchedOn >= from && d.WatchedOn < to)
                .Select(d => new DiaryRow(d.Id, d.MovieId, d.WatchedOn, d.Rewatch))
                .ToListAsync();
        }

        // Same definition of "the year's movies"; null below MinMovies.
        private async Task<double?> PreviousYearAverageAsync(string userId, int year)
        {
            var (from, to) = (new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var ids = (await DiaryAsync(userId, year)).Select(d => d.MovieId)
                .Concat(await _context.UserRatings
                    .Where(r => r.UserId == userId && r.CreatedAt >= from && r.CreatedAt < to)
                    .Select(r => r.MovieId)
                    .ToListAsync())
                .Distinct()
                .ToList();
            var values = await _context.UserRatings
                .Where(r => r.UserId == userId && ids.Contains(r.MovieId))
                .Select(r => r.Rating)
                .ToListAsync();
            return values.Count < MinMovies ? null : Math.Round(values.Average(), 1);
        }

        private async Task<List<int>> AvailableYearsAsync(string userId)
        {
            var diaryYears = await _context.DiaryEntries.Where(d => d.UserId == userId)
                .Select(d => d.WatchedOn.Year).Distinct().ToListAsync();
            var ratingYears = await _context.UserRatings.Where(r => r.UserId == userId && r.CreatedAt != null)
                .Select(r => r.CreatedAt!.Value.Year).Distinct().ToListAsync();
            return diaryYears.Union(ratingYears).OrderByDescending(y => y).ToList();
        }

        // Among the people the owner follows and the viewer may see: the most
        // compatible one with enough movies in common.
        private async Task<TasteMateDto?> TasteMateAsync(string viewerId, string ownerId)
        {
            var visible = _social.VisibleAuthors(viewerId).Select(u => u.Id);
            var followed = await _context.UserFollows
                .Where(f => f.FollowerId == ownerId && visible.Contains(f.FollowingId))
                .Select(f => f.FollowingId)
                .Take(MaxTasteMateCandidates)
                .ToListAsync();
            if (followed.Count == 0) return null;

            var best = (await _social.CompatibilityDetailsAsync(ownerId, followed))
                .Where(p => p.Value.CommonCount >= MinTasteMateCommon)
                .OrderByDescending(p => p.Value.Score).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => (UserId: p.Key, p.Value.Score, p.Value.CommonCount))
                .FirstOrDefault();
            if (best.UserId == null) return null;

            var user = await _context.Users.Where(u => u.Id == best.UserId)
                .Select(u => new UserBriefWithImageDto { Id = u.Id, UserName = u.UserName, ProfileImageUrl = u.ProfileImageUrl })
                .SingleAsync();
            return new TasteMateDto { User = user, Compatibility = best.Score, CommonCount = best.CommonCount };
        }

        private static int IsoWeekday(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        private static MovieSummaryDto Summary(MovieRow m) => new() { Id = m.Id, Title = m.Title, PosterPath = m.PosterPath };

        private static DatedMovieDto? Dated(DiaryRow row, Dictionary<int, MovieRow> movies) =>
            movies.TryGetValue(row.MovieId, out var m) ? new DatedMovieDto { Movie = Summary(m), WatchedOn = row.WatchedOn } : null;
    }
}
