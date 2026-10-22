namespace SineUyum.Api.Models
{
    // A weekly taste twin suggestion: once a week per user; the same person is
    // never suggested again.
    public class TasteTwinSuggestion
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public string SuggestedUserId { get; set; } = string.Empty;
        public AppUser SuggestedUser { get; set; } = null!;

        // Monday of the week (UTC).
        public DateOnly WeekStart { get; set; }

        public double Compatibility { get; set; }
        public int CommonMovieCount { get; set; }
        public bool Notified { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
