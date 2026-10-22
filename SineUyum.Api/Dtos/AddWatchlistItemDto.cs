// SineUyum.Api/Dtos/AddWatchlistItemDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class AddWatchlistItemDto
    {
        [Required]
        public int MovieId { get; set; }

        // Accepted for backward compatibility but IGNORED: movie metadata is
        // fetched from TMDb on the server (MovieCatalogService). Old clients
        // may keep sending it.
        public string? Title { get; set; }

        public string? PosterPath { get; set; }
    }
}
