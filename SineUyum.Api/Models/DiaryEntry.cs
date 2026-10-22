namespace SineUyum.Api.Models
{
    // A film diary entry: "I watched this on <date>". Separate from the rating
    // (the rating is the opinion, the diary is when it was watched); a movie
    // can have several entries (rewatches), but only one per day.
    public class DiaryEntry
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        // The calendar day it was watched (no time zone; stored as a date).
        public DateOnly WatchedOn { get; set; }

        public bool Rewatch { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
