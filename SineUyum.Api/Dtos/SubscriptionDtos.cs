using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class SubscriptionStatusDto
    {
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Provider { get; set; }
        // false: iptal edildi, ExpiresAt'te sona erecek.
        public bool WillRenew { get; set; }
        // Sunucuda yapılandırılmış ödeme sağlayıcıları (boşsa satın alınamaz).
        public List<string> AvailableProviders { get; set; } = new();
    }

    public class VerifyPurchaseDto
    {
        [Required]
        [MaxLength(32)]
        public string Provider { get; set; } = string.Empty;

        [Required]
        [MaxLength(4096)]
        public string PurchaseToken { get; set; } = string.Empty;
    }

    public class GrantSubscriptionDto
    {
        [Range(1, 366)]
        public int Days { get; set; }
    }
}
