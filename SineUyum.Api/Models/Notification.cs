// SineUyum.Api/Models/Notification.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // Rendered in the recipient's language when created. The source of
        // truth is MessageKey + MessageArgs; rows from before localization only
        // have this text.
        [Required]
        public string Message { get; set; } = string.Empty;

        // Locale key (e.g. "notifications.reviewLiked") and its arguments as a JSON array.
        [MaxLength(100)]
        public string? MessageKey { get; set; }
        public string? MessageArgs { get; set; }

        // Where tapping the notification leads (e.g. /profile/123).
        public string? RelatedUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
