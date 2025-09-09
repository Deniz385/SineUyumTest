// SineUyum.Api/Models/CinemaEvent.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SineUyum.Api.Models
{
    public class CinemaEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [MaxLength(200)]
        public string LocationName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Address { get; set; }

        [Required]
        public int GroupSize { get; set; } // Etkinlikteki masaların kaç kişilik olacağı

        // Bu etkinlik için seçilen film (oylama sonrası belirlenecek)
        public int? SelectedMovieId { get; set; }
        [ForeignKey("SelectedMovieId")]
        public Movie? SelectedMovie { get; set; }
    }
}