using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public enum SubscriptionStatus
    {
        // Paid; renews at the end of the period.
        Active,
        // The user canceled: still usable until the end of the period.
        Canceled,
        // The period ended and was not renewed.
        Expired,
        // The payment was refunded: ends immediately.
        Refunded,
    }

    // A subscription verified with a payment provider. This is the ONLY source
    // of the subscription state; whether a user is subscribed is computed from
    // here every time (by the end date). AppUser.IsSubscribed used to be a flag:
    // it could be turned on with a single request without paying and never expired.
    public class Subscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // "iyzico", "app_store", "google_play", "manual" (granted by an admin)...
        [Required]
        [MaxLength(32)]
        public string Provider { get; set; } = string.Empty;

        // The purchase/subscription id at the provider. (Provider, Reference) is
        // unique: the same payment cannot unlock a second account.
        [Required]
        [MaxLength(200)]
        public string ProviderReference { get; set; } = string.Empty;

        public SubscriptionStatus Status { get; set; }

        public DateTime StartedAt { get; set; }

        // Entitled until this date (even if canceled).
        public DateTime CurrentPeriodEnd { get; set; }

        public DateTime? CanceledAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsEntitledAt(DateTime now) =>
            (Status == SubscriptionStatus.Active || Status == SubscriptionStatus.Canceled)
            && CurrentPeriodEnd > now;
    }
}
