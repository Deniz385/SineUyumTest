using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SineUyum.Api.Dtos
{
    // Clients have historically sent {id, title, posterPath} to this endpoint.
    // Only the id is read; title/poster are ignored because they come from TMDb.
    public class AddMovieDto
    {
        [Required]
        [JsonPropertyName("id")]
        public int MovieId { get; set; }
    }
}
