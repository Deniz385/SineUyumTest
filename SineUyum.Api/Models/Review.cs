namespace SineUyum.Api.Models
{
    // Bir filme yazılan inceleme. Puan UserRatings'te kalır; inceleme o puanın
    // yazılı hâlidir (kullanıcı ve film başına tek).
    public class Review
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public string Text { get; set; } = string.Empty;
        public bool ContainsSpoilers { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ReviewLike> Likes { get; set; } = new List<ReviewLike>();
        public ICollection<ReviewComment> Comments { get; set; } = new List<ReviewComment>();
    }

    public class ReviewLike
    {
        public int ReviewId { get; set; }
        public Review Review { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ReviewComment
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }
        public Review Review { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
