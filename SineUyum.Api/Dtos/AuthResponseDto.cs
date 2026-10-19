namespace SineUyum.Api.Dtos
{
    // Mobil istemcinin tip güvenli şekilde okuyabilmesi için tanımlanmış
    // kimlik doğrulama yanıtı. Anonim obje yerine bu DTO kullanılır ki
    // Swagger/OpenAPI şeması da doğru üretilsin.
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
    }
}
