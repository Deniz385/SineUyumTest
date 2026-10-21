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
        // Yazarın bu filme verdiği güncel puan.
        public int? Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool ContainsSpoilers { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool LikedByMe { get; set; }
        public bool IsMine { get; set; }
        // Görüntüleyenle yazarın uyumu (0-100); ortak film yoksa ya da kendi
        // incelemesiyse null.
        public double? AuthorCompatibility { get; set; }
    }

    // PUT /api/v1/reviews/movie/{movieId}
    public class UpsertReviewDto
    {
        [Required(ErrorMessage = "İnceleme metni boş olamaz.")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "İnceleme 1-2000 karakter olmalı.")]
        public string Text { get; set; } = string.Empty;
        public bool ContainsSpoilers { get; set; }
    }

    // GET /api/v1/reviews/movie/{movieId}
    public class MovieReviewsDto
    {
        public int? MyRating { get; set; }
        public ReviewDto? Mine { get; set; }
        public List<ReviewDto> Reviews { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class ReviewCommentDto
    {
        public int Id { get; set; }
        public UserBriefWithImageDto Author { get; set; } = new();
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        // Yorumun ya da incelemenin sahibi silebilir.
        public bool CanDelete { get; set; }
    }

    public class CreateCommentDto
    {
        [Required(ErrorMessage = "Yorum boş olamaz.")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Yorum 1-500 karakter olmalı.")]
        public string Text { get; set; } = string.Empty;
    }

    public class LikeResultDto
    {
        public bool Liked { get; set; }
        public int LikeCount { get; set; }
    }

    // GET /api/v1/feed. Type: "Rated" (yalnızca puan) | "Reviewed" (inceleme).
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
        // Sonraki sayfa için ?cursor= değeri; son sayfada null.
        public string? NextCursor { get; set; }
    }
}
