// SineUyum.Api/Models/Movie.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class Movie
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // ID'nin veritabanı tarafından oluşturulmayacağını belirtir
        public int Id { get; set; } // Bu ID, TMDb'deki film ID'si ile aynı olacak

        public string Title { get; set; } = string.Empty;

        public string? PosterPath { get; set; }

        // Türler TMDb'den en son ne zaman işlendi (null: hiç). Tür bilgisi
        // önceden hiç yazılmıyordu; eski filmler arka planda tamamlanır.
        public DateTime? GenresSyncedAt { get; set; }

        // TMDb genel ortalaması (0-10); "en aykırı puanın" bununla bulunur.
        public double? TmdbVoteAverage { get; set; }

        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    }
}