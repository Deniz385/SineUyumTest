namespace SineUyum.Api.Models
{
    // Models/AppUser.cs
    using Microsoft.AspNetCore.Identity;

    public class AppUser : IdentityUser
    {
        public string? ProfileImageUrl { get; set; } // Profil fotoğrafının URL'si (boş olabilir)
        public string? Bio { get; set; } 
        public bool IsSubscribed { get; set; } = false;
        public DateTime? SubscriptionExpires { get; set; }
        public ICollection<UserRating> Ratings { get; set; } = new List<UserRating>();
        // IdentityUser sınıfı zaten Id, UserName, Email, PasswordHash gibi
        // birçok özelliği kendisi içeriyor.
        // Buraya daha sonra profil resmi, doğum tarihi gibi
        // ekstra özellikler ekleyebiliriz.
    }
}
