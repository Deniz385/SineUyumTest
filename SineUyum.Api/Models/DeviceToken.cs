using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    // A device registration for push notifications (FCM). The mobile app saves
    // it after sign-in and whenever the FCM token is refreshed, and deletes it on
    // logout. A token belongs to one user: if another account signs in on the
    // same device the row moves to the new user (so the previous user's
    // notifications stop going to that device).
    public class DeviceToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // The FCM registration token. It is the value sent to FCM, so it is kept as
        // is (it cannot be hashed like a refresh token).
        [Required]
        [MaxLength(512)]
        public string Token { get; set; } = string.Empty;

        // "android" | "ios"
        [Required]
        [MaxLength(16)]
        public string Platform { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // The app re-registers on every start; tokens not seen for a long time can
        // be cleaned up by this field later.
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
