using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    // Mobil istemcinin uzun süreli oturumu için kullanılan, sunucu tarafından
    // iptal edilebilir yenileme anahtarı. Access token kısa ömürlü tutulur,
    // süresi dolduğunda bu token ile yenisi alınır.
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // Token'ın kendisi veritabanında düz metin tutulmaz; SHA-256 özeti saklanır.
        [Required]
        [MaxLength(128)]
        public string TokenHash { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RevokedAt { get; set; }

        // Hangi cihazdan açıldığı (mobil / web ayrımı ve oturum listesi için)
        [MaxLength(200)]
        public string? DeviceInfo { get; set; }

        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
