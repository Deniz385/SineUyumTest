namespace SineUyum.Api.Models
{
    // Models/AppUser.cs
    using Microsoft.AspNetCore.Identity;

    public class AppUser : IdentityUser
    {
        public string? ProfileImageUrl { get; set; } // Profile photo URL (may be empty)
        public string? Bio { get; set; }
        // The subscription state is NOT stored here: it is computed from the
        // end dates in the Subscriptions table (SubscriptionService).
        // Privacy settings (G12); the defaults match the previous behaviour.
        public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
        public MessagePermission MessagePermission { get; set; } = MessagePermission.Everyone;
        public bool HiddenFromSearch { get; set; }
        // Read receipts are mutual: whoever turns them off neither shows their
        // own reads nor sees anyone else's (M10).
        public bool ShowReadReceipts { get; set; } = true;
        // Two-letter language code ("en", "tr"). Notifications and pushes are
        // rendered in it. New accounts take the request language; accounts
        // created before localization were migrated to "tr".
        public string Language { get; set; } = Localization.SupportedLanguages.Default;
        public ICollection<UserRating> Ratings { get; set; } = new List<UserRating>();
        // IdentityUser already provides Id, UserName, Email, PasswordHash and
        // many other properties. Extra profile fields such as a birth date
        // can be added here later.
    }
}
