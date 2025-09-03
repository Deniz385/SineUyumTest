namespace SineUyum.Api.Models
{
    // Models/AppUser.cs
    using Microsoft.AspNetCore.Identity;

    public class AppUser : IdentityUser
    {
        // IdentityUser sınıfı zaten Id, UserName, Email, PasswordHash gibi
        // birçok özelliği kendisi içeriyor.
        // Buraya daha sonra profil resmi, doğum tarihi gibi
        // ekstra özellikler ekleyebiliriz.
    }
}
