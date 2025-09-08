// SineUyum.Api/Models/WatchlistItem.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class WatchlistItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty; // Başlangıç değeri atandı
        public AppUser User { get; set; } = null!; // Başlangıç değeri atandı (null forgiving operator)

        [Required]
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!; // Başlangıç değeri atandı

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}