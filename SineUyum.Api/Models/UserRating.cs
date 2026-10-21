namespace SineUyum.Api.Models
{
    public class UserRating
    {
        public int Id { get; set; }
        public int Rating { get; set; }

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        // İlk puanlama anı. Bu alanlar akışla birlikte eklendi; önceki
        // puanlarda boştur ve akışta görünmezler.
        public DateTime? CreatedAt { get; set; }

        // Akıştaki sıralama anı: ilk puanlama ya da ilk inceleme. Puanı
        // değiştirmek ya da incelemeyi düzenlemek öne taşımaz.
        public DateTime? ActivityAt { get; set; }
    }
}
