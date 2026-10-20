namespace SineUyum.Api.Models
{
    // Models/AppUser.cs
    using Microsoft.AspNetCore.Identity;

    public class AppUser : IdentityUser
    {
        public string? ProfileImageUrl { get; set; } // Profil fotoğrafının URL'si (boş olabilir)
        public string? Bio { get; set; } 
        // Abonelik durumu burada TUTULMAZ: Subscriptions tablosundan bitiş
        // tarihine bakılarak hesaplanır (SubscriptionService).
        // Gizlilik ayarları (G12); varsayılanlar mevcut davranışla aynı.
        public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
        public MessagePermission MessagePermission { get; set; } = MessagePermission.Everyone;
        public bool HiddenFromSearch { get; set; }
        // Okundu bilgisi karşılıklıdır: kapatan, kendi okumasını göstermez ve
        // başkalarınınkini de görmez (M10).
        public bool ShowReadReceipts { get; set; } = true;
        public ICollection<UserRating> Ratings { get; set; } = new List<UserRating>();
        // IdentityUser sınıfı zaten Id, UserName, Email, PasswordHash gibi
        // birçok özelliği kendisi içeriyor.
        // Buraya daha sonra profil resmi, doğum tarihi gibi
        // ekstra özellikler ekleyebiliriz.
    }
}
