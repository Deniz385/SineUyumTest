// SineUyum.Api/Models/MovieGenre.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class MovieGenre
    {
        public int MovieId { get; set; }
        [ForeignKey("MovieId")]
        public Movie Movie { get; set; } = null!;

        public int GenreId { get; set; }
        [ForeignKey("GenreId")]
        public Genre Genre { get; set; } = null!;
    }
}