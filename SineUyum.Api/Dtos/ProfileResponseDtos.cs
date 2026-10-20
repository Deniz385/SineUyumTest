namespace SineUyum.Api.Dtos
{
    // GET /api/v1/profile/{userId}
    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
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

    // GET /api/v1/compatibility/{targetUserId}
    public class CompatibilityDto
    {
        public double CompatibilityScore { get; set; }
        public int CommonMovieCount { get; set; }
        public List<CommonMovieDto> CommonMovies { get; set; } = new();
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
