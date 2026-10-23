// SineUyum.Api/Models/Watchlist.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

        // Public lists can be discovered, liked and saved by others (if the
        // owner's profile details are visible to them).
        public WatchlistVisibility Visibility { get; set; } = WatchlistVisibility.Private;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // When the list was last made public ("new" in discovery).
        public DateTime? PublishedAt { get; set; }

        public ICollection<WatchlistLike> Likes { get; set; } = new List<WatchlistLike>();
        public ICollection<WatchlistSave> Saves { get; set; } = new List<WatchlistSave>();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WatchlistVisibility
    {
        // Only the owner, the members and people it was sent to in a message.
        Private = 0,
        Public = 1
    }

    public class WatchlistLike
    {
        public int WatchlistId { get; set; }
        public Watchlist Watchlist { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // A saved (followed) list shows up in the user's saved lists.
    public class WatchlistSave
    {
        public int WatchlistId { get; set; }
        public Watchlist Watchlist { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}