// SineUyum.Api/Models/Watchlist.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class Watchlist
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // Bir listenin içinde birden çok film olabilir
        public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
    }
}