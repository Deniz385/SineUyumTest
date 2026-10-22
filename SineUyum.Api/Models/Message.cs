// SineUyum.Api/Models/Message.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;
        public AppUser Sender { get; set; } = null!;

        [Required]
        public string RecipientId { get; set; } = string.Empty;
        public AppUser Recipient { get; set; } = null!;

        // Content is optional (nullable) so a movie suggestion can be sent on its own.
        public string? Content { get; set; }

        public DateTime? DateRead { get; set; }
        
        [Required]
        public DateTime MessageSent { get; set; } = DateTime.UtcNow;

        // Fields for suggesting a movie
        public int? MovieId { get; set; }
        [ForeignKey("MovieId")]
        public Movie? Movie { get; set; }
         public int? WatchlistId { get; set; } // Optional list id
        [ForeignKey("WatchlistId")]
        public Watchlist? Watchlist { get; set; }
    }
}