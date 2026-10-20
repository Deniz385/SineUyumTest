using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    // Push bildirimi (FCM) için cihaz kaydı. Mobil uygulama oturum açınca ve
    // FCM token'ı yenilenince kaydeder; çıkışta siler. Bir token tek bir
    // kullanıcıya aittir: aynı cihazda başka hesapla giriş yapılırsa kayıt
    // yeni kullanıcıya taşınır (önceki kullanıcının bildirimleri o cihaza
    // gitmeye devam etmesin).
    public class DeviceToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // FCM kayıt token'ı. FCM'e gönderilen değer olduğu için düz tutulur
        // (refresh token gibi özetlenemez).
        [Required]
        [MaxLength(512)]
        public string Token { get; set; } = string.Empty;

        // "android" | "ios"
        [Required]
        [MaxLength(16)]
        public string Platform { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Uygulama her açılışta yeniden kaydeder; uzun süre görülmeyen
        // token'lar ileride bu alana göre temizlenebilir.
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
