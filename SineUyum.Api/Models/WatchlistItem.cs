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
    }
}