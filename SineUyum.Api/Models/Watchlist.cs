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

        // A list can contain many movies
        public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();

        // Shared list members (invited and accepted).
        public ICollection<WatchlistMember> Members { get; set; } = new List<WatchlistMember>();
    }
}