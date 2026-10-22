// SineUyum.Api/Models/WatchlistItem.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class WatchlistItem
    {
        [Key]
        public int Id { get; set; }

        // --- DEĞİŞİKLİK: UserId yerine WatchlistId geldi ---
        [Required]
        public int WatchlistId { get; set; }
        public Watchlist Watchlist { get; set; } = null!;

        [Required]
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Ortak listede filmi kimin eklediği (eski kayıtlarda boş: sahip).
        public string? AddedByUserId { get; set; }
        public AppUser? AddedBy { get; set; }
    }
}