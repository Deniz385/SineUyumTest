namespace SineUyum.Api.Models
{// Models/UserRating.cs
    public class UserRating
    {
        public int Id { get; set; }
        public int Rating { get; set; }

        // --- İlişkiler (Foreign Keys & Navigation Properties) ---

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        // --- BU KISIM DEĞİŞTİ ---
        // Identity'nin kullanıcı ID'si string tipindedir.
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
    }
}
