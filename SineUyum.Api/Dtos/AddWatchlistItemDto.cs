// SineUyum.Api/Dtos/AddWatchlistItemDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class AddWatchlistItemDto
    {
        [Required]
        public int MovieId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty; // Başlangıç değeri atandı

        public string? PosterPath { get; set; }
    }
}