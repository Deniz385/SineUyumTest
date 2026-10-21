namespace SineUyum.Api.Models
{
    // Haftalık zevk ikizi önerisi: kullanıcı başına haftada bir; aynı kişi
    // bir daha önerilmez.
    public class TasteTwinSuggestion
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public string SuggestedUserId { get; set; } = string.Empty;
        public AppUser SuggestedUser { get; set; } = null!;

        // Haftanın pazartesisi (UTC).
        public DateOnly WeekStart { get; set; }

        public double Compatibility { get; set; }
        public int CommonMovieCount { get; set; }
        public bool Notified { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
