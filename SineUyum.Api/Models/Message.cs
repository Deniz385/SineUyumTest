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

        // Film önerisi gönderilebilmesi için Content artık zorunlu değil (nullable).
        public string? Content { get; set; }

        public DateTime? DateRead { get; set; }
        
        [Required]
        public DateTime MessageSent { get; set; } = DateTime.UtcNow;

        // Film önerme özelliği için yeni alanlar
        public int? MovieId { get; set; }
        [ForeignKey("MovieId")]
        public Movie? Movie { get; set; }
         public int? WatchlistId { get; set; } // Opsiyonel liste ID'si
        [ForeignKey("WatchlistId")]
        public Watchlist? Watchlist { get; set; }
    }
}