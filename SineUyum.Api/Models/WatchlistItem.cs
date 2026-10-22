// SineUyum.Api/Models/WatchlistItem.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class WatchlistItem
    {
        [Key]
        public int Id { get; set; }

        // --- CHANGE: WatchlistId replaced UserId ---
        [Required]
        public int WatchlistId { get; set; }
        public Watchlist Watchlist { get; set; } = null!;

        [Required]
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Who added the movie to a shared list (empty in older rows: the owner).
        public string? AddedByUserId { get; set; }
        public AppUser? AddedBy { get; set; }
    }
}