// SineUyum.Api/Models/Genre.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class Genre
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // ID'yi biz vereceğiz (TMDb'den gelen)
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // Bir türün birden çok filmi olabilir (MovieGenre ara tablosu üzerinden)
        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    }
}