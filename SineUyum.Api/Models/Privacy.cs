using System.Text.Json.Serialization;

namespace SineUyum.Api.Models
{
    // Profil ayrıntılarını (puanlar, istatistikler, takip listeleri, uyum)
    // kimin görebileceği. Ad, fotoğraf ve biyografi her zaman görünür ki
    // kullanıcı kimi engellediğini/kime yazdığını bilsin.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProfileVisibility
    {
        Public,
        // Yalnızca profil sahibinin takip ettiği kişiler. Takip onaysız olduğu
        // için "takipçilerim" seçeneği koruma sağlamazdı; kontrol sahipte kalır.
        Following,
        Private
    }

    // Kimin yeni mesaj gönderebileceği.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessagePermission
    {
        Everyone,
        Following,
        Nobody
    }

    // Engelleme tek yönlü kaydedilir, etkisi iki yönlüdür: taraflar birbirinin
    // profilini göremez, mesajlaşamaz, takip edemez, aramada ve etkinlik
    // eşleştirmesinde bir araya gelmez.
    public class UserBlock
    {
        public string BlockerId { get; set; } = string.Empty;
        public AppUser Blocker { get; set; } = null!;

        public string BlockedId { get; set; } = string.Empty;
        public AppUser Blocked { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
