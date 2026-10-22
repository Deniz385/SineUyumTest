namespace SineUyum.Api.Models
{
    public class UserRating
    {
        public int Id { get; set; }
        public int Rating { get; set; }

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // When it was first rated. These fields came with the feed; older ratings
        // have them empty and do not appear in the feed.
        public DateTime? CreatedAt { get; set; }

        // Ordering time in the feed: the first rating or the first review. Changing
        // the rating or editing the review does not move it up.
        public DateTime? ActivityAt { get; set; }
    }
}
