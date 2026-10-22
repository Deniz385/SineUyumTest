// SineUyum.Api/Models/Genre.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class Genre
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // We assign the id ourselves (it comes from TMDb)
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // A genre can have many movies (through the MovieGenre join table)
        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    }
}