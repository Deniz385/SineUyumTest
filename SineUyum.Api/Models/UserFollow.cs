// SineUyum.Api/Models/UserFollow.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class UserFollow
    {
        [Key]
        public int Id { get; set; }

        // Takip eden kullanıcı
        [Required]
        public string FollowerId { get; set; } = string.Empty; // Başlangıç değeri atandı
        public AppUser Follower { get; set; } = null!; // Başlangıç değeri atandı

        // Takip edilen kullanıcı
        [Required]
        public string FollowingId { get; set; } = string.Empty; // Başlangıç değeri atandı
        public AppUser Following { get; set; } = null!; // Başlangıç değeri atandı
    }
}