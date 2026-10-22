using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class SubscriptionStatusDto
    {
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Provider { get; set; }
        // false: canceled, ends at ExpiresAt.
        public bool WillRenew { get; set; }
        // Payment providers configured on the server (none: nothing can be bought).
        public List<string> AvailableProviders { get; set; } = new();
    }

    public class VerifyPurchaseDto
    {
        [Required(ErrorMessage = "validation.required")]
        [MaxLength(32, ErrorMessage = "validation.maxLength")]
        public string Provider { get; set; } = string.Empty;

        [Required(ErrorMessage = "validation.required")]
        [MaxLength(4096, ErrorMessage = "validation.maxLength")]
        public string PurchaseToken { get; set; } = string.Empty;
    }

    public class GrantSubscriptionDto
    {
        [Range(1, 366, ErrorMessage = "validation.range")]
        public int Days { get; set; }
    }
}
