// SineUyum.Api/Models/UserFollow.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class UserFollow
    {
        [Key]
        public int Id { get; set; }

        // The user who follows
        [Required]
        public string FollowerId { get; set; } = string.Empty; // Initial value assigned
        public AppUser Follower { get; set; } = null!; // Initial value assigned

        // The user being followed
        [Required]
        public string FollowingId { get; set; } = string.Empty; // Initial value assigned
        public AppUser Following { get; set; } = null!; // Initial value assigned
    }
}