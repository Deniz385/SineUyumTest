// SineUyum.Api/Models/EventVote.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class EventVote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EventGroupId { get; set; }
        [ForeignKey("EventGroupId")]
        public EventGroup EventGroup { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;

        [Required]
        public int MovieId { get; set; }
        [ForeignKey("MovieId")]
        public Movie Movie { get; set; } = null!;
    }
}