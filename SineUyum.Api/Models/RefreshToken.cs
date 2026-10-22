using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    // A server-revocable refresh token for the mobile client's long-lived
    // session. The access token is short-lived; when it expires a new one is
    // obtained with this token.
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // The token itself is never stored in plain text; its SHA-256 hash is.
        [Required]
        [MaxLength(128)]
        public string TokenHash { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RevokedAt { get; set; }

        // Which device opened it (to tell mobile from web and for a session list)
        [MaxLength(200)]
        public string? DeviceInfo { get; set; }

        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
