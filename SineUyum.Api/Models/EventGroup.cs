// SineUyum.Api/Models/EventGroup.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class EventGroup
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CinemaEventId { get; set; }
        [ForeignKey("CinemaEventId")]
        public CinemaEvent CinemaEvent { get; set; } = null!;

        // Gruba önerilen filmlerin ID'leri (virgülle ayrılmış)
        public string? SuggestedMovieIds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EventGroupMember> Members { get; set; } = new List<EventGroupMember>();
    }
}