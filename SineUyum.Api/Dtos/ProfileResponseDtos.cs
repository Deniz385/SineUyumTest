namespace SineUyum.Api.Dtos
{
    // GET /api/v1/profile/{userId}
    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        // true: the viewer cannot see the details (privacy setting, or the viewer
        // blocked them); ratings and statistics are empty.
        public bool IsRestricted { get; set; }
        // The viewer has blocked this user (for the unblock button).
        public bool IsBlockedByMe { get; set; }
        public List<ProfileRatingDto> Ratings { get; set; } = new();
        public ProfileStatisticsDto Statistics { get; set; } = new();
    }

    public class ProfileRatingDto
    {
        public int MovieId { get; set; }
        public int Rating { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
    }

    public class ProfileStatisticsDto
    {
        public int TotalRatings { get; set; }
        public double AverageRating { get; set; }
        public int TotalMoviesInWatchlists { get; set; }
        public List<ProfileRatingDto> TopRatedMovies { get; set; } = new();
    }

    // POST/DELETE /api/v1/profile/photo. Uploaded photos are server-relative
    // paths ("/media/avatars/..."); the client joins them with the API address.
    public class ProfilePhotoDto
    {
        public string? ProfileImageUrl { get; set; }
    }

    // GET /api/v1/compatibility/{targetUserId}
    public class CompatibilityDto
    {
        public double CompatibilityScore { get; set; }
        public int CommonMovieCount { get; set; }
        public List<CommonMovieDto> CommonMovies { get; set; } = new();
        // Explains the score (shared/divergent genres, shared favorite, debate movie).
        public CompatibilityInsightsDto Insights { get; set; } = new();
    }

    public class CompatibilityInsightsDto
    {
        // A one-sentence summary in the request language; null when there is nothing to say.
        public string? Summary { get; set; }
        public List<string> SharedGenres { get; set; } = new();
        public List<string> DivergentGenres { get; set; } = new();
        // A common movie both of them loved.
        public CommonMovieDto? SharedFavorite { get; set; }
        // The common movie they disagree on most: a conversation starter.
        public CommonMovieDto? DebateMovie { get; set; }
    }

    public class CommonMovieDto
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int CurrentUserRating { get; set; }
        public int TargetUserRating { get; set; }
    }
}

namespace SineUyum.Api.Dtos
{
    // GET /api/v1/profile/{userId}/identity — the shareable film identity (texts in the request language).
    public class FilmIdentityDto
    {
        public string UserName { get; set; } = string.Empty;
        public int TotalRatings { get; set; }
        public double AverageRating { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleDescription { get; set; } = string.Empty;
        public List<GenreShareDto> TopGenres { get; set; } = new();
        // Average distance of the ratings from the TMDb average (at least 8 movies).
        public double? Deviation { get; set; }
        // The movie that departs most from the consensus (at least 3 points apart).
        public IdentityMovieDto? ContrarianTake { get; set; }
        public IdentityMovieDto? Favorite { get; set; }
    }

    public class GenreShareDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Average { get; set; }
        // Percentage among the movies with a known genre.
        public int Share { get; set; }
    }

    public class IdentityMovieDto
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int Rating { get; set; }
        public double? TmdbAverage { get; set; }
    }
}
