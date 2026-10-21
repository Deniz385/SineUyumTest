namespace SineUyum.Api.Dtos
{
    // GET /api/v1/profile/{userId}
    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        // true: görüntüleyen ayrıntıları göremez (gizlilik ayarı ya da kendisi
        // engellemiş); puanlar ve istatistikler boş döner.
        public bool IsRestricted { get; set; }
        // Görüntüleyen bu kullanıcıyı engellemiş (engeli kaldırma düğmesi için).
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

    // POST/DELETE /api/v1/profile/photo. Yüklenen fotoğraflar sunucuya göreli
    // yoldur ("/media/avatars/..."); istemci API adresiyle birleştirir.
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
        // Puanın açıklaması (ortak/ayrışan türler, ortak favori, tartışma filmi).
        public CompatibilityInsightsDto Insights { get; set; } = new();
    }

    public class CompatibilityInsightsDto
    {
        // Tek cümlelik özet; söylenecek bir şey yoksa null.
        public string? Summary { get; set; }
        public List<string> SharedGenres { get; set; } = new();
        public List<string> DivergentGenres { get; set; } = new();
        // İkisinin de çok sevdiği ortak film.
        public CommonMovieDto? SharedFavorite { get; set; }
        // En çok ayrıştıkları ortak film: sohbet başlatıcı.
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
