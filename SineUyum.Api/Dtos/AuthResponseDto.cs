using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    // A typed authentication response the mobile client can read safely. This
    // DTO is used instead of an anonymous object so the Swagger/OpenAPI schema
    // is generated correctly.
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserSummaryDto User { get; set; } = new();
    }

    public class UserSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public bool IsSubscribed { get; set; }
        public DateTime? SubscriptionExpires { get; set; }
        public List<string> Roles { get; set; } = new();
        // The user's stored language ("en", "tr"); notifications use it.
        public string Language { get; set; } = string.Empty;
    }

    // PUT /api/v1/account/language request and response.
    public class LanguageDto
    {
        [Required(ErrorMessage = "validation.required")]
        [MaxLength(16, ErrorMessage = "validation.maxLength")]
        public string Language { get; set; } = string.Empty;
    }
}
