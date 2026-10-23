using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class UserBriefWithImageDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    public class ReviewDto
    {
        public int Id { get; set; }
        public MovieSummaryDto Movie { get; set; } = new();
        public UserBriefWithImageDto Author { get; set; } = new();
        // The author's current rating of this movie.
        public int? Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool ContainsSpoilers { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool LikedByMe { get; set; }
        public bool IsMine { get; set; }
        // Compatibility between the viewer and the author (0-100); null without
        // common movies or for the viewer's own review.
        public double? AuthorCompatibility { get; set; }
    }

    // PUT /api/v1/reviews/movie/{movieId}
    public class UpsertReviewDto
    {
        [Required(ErrorMessage = "reviews.textRequired")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "reviews.textLength")]
        public string Text { get; set; } = string.Empty;
        public bool ContainsSpoilers { get; set; }
    }

    // GET /api/v1/reviews/movie/{movieId}
    // GET /api/v1/reviews/user/{userId}
    public class ReviewPageDto
    {
        public List<ReviewDto> Items { get; set; } = new();
        public int Page { get; set; }
        public bool HasMore { get; set; }
    }

    public class MovieReviewsDto
    {
        public int? MyRating { get; set; }
        public ReviewDto? Mine { get; set; }
        public List<ReviewDto> Reviews { get; set; } = new();
        public int TotalCount { get; set; }
        // Score based on people compatible with you; null without enough data.
        public ForYouScoreDto? ForYou { get; set; }
    }

    public class ForYouScoreDto
    {
        public double Score { get; set; }
        // Number of compatible people taken into account.
        public int BasedOn { get; set; }
        // The three most compatible people and their ratings.
        public List<ForYouRaterDto> TopMatches { get; set; } = new();
    }

    public class ForYouRaterDto
    {
        public UserBriefWithImageDto Author { get; set; } = new();
        public int Rating { get; set; }
        public double Compatibility { get; set; }
    }

    public class ReviewCommentDto
    {
        public int Id { get; set; }
        public UserBriefWithImageDto Author { get; set; } = new();
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        // The owner of the comment or of the review can delete it.
        public bool CanDelete { get; set; }
    }

    public class CreateCommentDto
    {
        [Required(ErrorMessage = "reviews.commentRequired")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "reviews.commentLength")]
        public string Text { get; set; } = string.Empty;
    }

    public class LikeResultDto
    {
        public bool Liked { get; set; }
        public int LikeCount { get; set; }
    }

    // GET /api/v1/feed. Type: "Rated" (rating only) | "Reviewed" (review).
    public class FeedItemDto
    {
        public string Type { get; set; } = string.Empty;
        public DateTime ActivityAt { get; set; }
        public UserBriefWithImageDto Author { get; set; } = new();
        public MovieSummaryDto Movie { get; set; } = new();
        public int Rating { get; set; }
        public ReviewDto? Review { get; set; }
        public double? AuthorCompatibility { get; set; }
    }

    public class FeedPageDto
    {
        public List<FeedItemDto> Items { get; set; } = new();
        // The ?cursor= value for the next page; null on the last page.
        public string? NextCursor { get; set; }
    }
}

namespace SineUyum.Api.Dtos
{
    // GET /api/v1/taste-twin — this week's taste twin (204 if none).
    public class TasteTwinDto
    {
        public UserBriefWithImageDto User { get; set; } = new();
        public string? Bio { get; set; }
        public double Compatibility { get; set; }
        public int CommonMovieCount { get; set; }
        public DateOnly WeekStart { get; set; }
        public bool IsFollowing { get; set; }
    }
}
