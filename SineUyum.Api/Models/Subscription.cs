using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public enum SubscriptionStatus
    {
        // Ödendi, dönem sonunda yenilenecek.
        Active,
        // Kullanıcı iptal etti: dönem sonuna kadar hâlâ kullanabilir.
        Canceled,
        // Dönem bitti ve yenilenmedi.
        Expired,
        // Ödeme iade edildi: hemen sona erer.
        Refunded,
    }

    // Bir ödeme sağlayıcısında doğrulanmış abonelik. Abonelik durumunun TEK
    // kaynağı burasıdır; kullanıcının abone olup olmadığı her seferinde buradan
    // (bitiş tarihine bakılarak) hesaplanır. Önceden AppUser.IsSubscribed bir
    // bayraktı: ödemesiz tek istekle açılabiliyor ve hiç sona ermiyordu.
    public class Subscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // "iyzico", "app_store", "google_play", "manual" (yönetici tanımladı)...
        [Required]
        [MaxLength(32)]
        public string Provider { get; set; } = string.Empty;

        // Sağlayıcıdaki satın alma/abonelik kimliği. (Provider, Reference)
        // benzersizdir: aynı ödeme ikinci bir hesabı açamaz.
        [Required]
        [MaxLength(200)]
        public string ProviderReference { get; set; } = string.Empty;

        public SubscriptionStatus Status { get; set; }

        public DateTime StartedAt { get; set; }

        // Bu tarihe kadar kullanım hakkı var (iptal edilse bile).
        public DateTime CurrentPeriodEnd { get; set; }

        public DateTime? CanceledAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsEntitledAt(DateTime now) =>
            (Status == SubscriptionStatus.Active || Status == SubscriptionStatus.Canceled)
            && CurrentPeriodEnd > now;
    }
}
