namespace SineUyum.Api.Dtos
{
    // GET /api/v1/year-in-review/user/{userId}?year=2026: a year of movies,
    // built from the diary (watch dates) and the ratings given that year.
    // Texts that need grammar (the yearly title) come localized; numbers,
    // months and weekdays are formatted by the clients.
    public class YearInReviewDto
    {
        public int Year { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool IsMine { get; set; }
        // False when the year has too few movies for a meaningful summary
        // (the other fields are still filled where possible).
        public bool Ready { get; set; }
        public int MinMovies { get; set; }
        // Years with any activity, newest first.
        public List<int> AvailableYears { get; set; } = new();

        public YearTotalsDto Totals { get; set; } = new();
        // 12 entries: diary entries per month, plus ratings of movies that have
        // no diary entry that year (by rating date).
        public List<MonthCountDto> Months { get; set; } = new();
        public MonthCountDto? BusiestMonth { get; set; }
        // ISO weekday (1 = Monday ... 7 = Sunday); from the diary, at least 3 entries.
        public WeekdayCountDto? FavoriteWeekday { get; set; }

        public double? AverageRating { get; set; }
        public double? PreviousYearAverage { get; set; }
        // The year's taste profile: title, top genres, favorite, contrarian take.
        public FilmIdentityDto Identity { get; set; } = new();
        // Highest rated movies of the year (up to 5).
        public List<IdentityMovieDto> TopMovies { get; set; } = new();

        public DatedMovieDto? FirstMovie { get; set; }
        public DatedMovieDto? LastMovie { get; set; }
        public RewatchedMovieDto? MostRewatched { get; set; }

        // The most compatible person the user follows (at least 5 movies in common).
        public TasteMateDto? TasteMate { get; set; }
    }

    public class YearTotalsDto
    {
        // Distinct movies watched (diary) or rated that year.
        public int Movies { get; set; }
        public int DiaryEntries { get; set; }
        public int Rewatches { get; set; }
        public int Ratings { get; set; }
        public int Reviews { get; set; }
        public int LikesReceived { get; set; }
        public int CommentsReceived { get; set; }
    }

    public class MonthCountDto
    {
        public int Month { get; set; }
        public int Count { get; set; }
    }

    public class WeekdayCountDto
    {
        public int Weekday { get; set; }
        public int Count { get; set; }
    }

    public class DatedMovieDto
    {
        public MovieSummaryDto Movie { get; set; } = new();
        public DateOnly WatchedOn { get; set; }
    }

    public class RewatchedMovieDto
    {
        public MovieSummaryDto Movie { get; set; } = new();
        public int Count { get; set; }
    }

    public class TasteMateDto
    {
        public UserBriefWithImageDto User { get; set; } = new();
        public double Compatibility { get; set; }
        public int CommonCount { get; set; }
    }
}
