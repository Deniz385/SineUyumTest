// SineUyum.Api/Models/EventParticipant.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class EventParticipant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;

        // Hangi etkinliğe katılmak istediği
        [Required]
        public int CinemaEventId { get; set; }
        [ForeignKey("CinemaEventId")]
        public CinemaEvent CinemaEvent { get; set; } = null!;
        
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}