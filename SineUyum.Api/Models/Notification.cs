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

        [Required]
        public string Message { get; set; } = string.Empty;

        // --- EKLENEN SATIR ---
        // Bildirime tıklandığında gidilecek olan URL (örn: /profile/123)
        public string? RelatedUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

