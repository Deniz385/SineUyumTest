// SineUyum.Api/Models/EventGroupMember.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class EventGroupMember
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EventGroupId { get; set; }
        [ForeignKey("EventGroupId")]
        public EventGroup EventGroup { get; set; } = null!;
        
        [Required]
        public string UserId { get; set; } = string.Empty; // <-- DEĞİŞİKLİK BURADA
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;
    }
}