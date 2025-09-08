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
    }
}