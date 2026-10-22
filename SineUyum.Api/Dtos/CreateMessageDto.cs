// SineUyum.Api/Dtos/CreateMessageDto.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SineUyum.Api.Dtos
{
    public class CreateMessageDto
    {
        [Required(ErrorMessage = "validation.required")]
        public string RecipientId { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "validation.maxLength")]
        public string? Content { get; set; }

        // Binds the "movieId" field of the incoming JSON to this property.
        [JsonPropertyName("movieId")]
        public int? MovieId { get; set; }
        [JsonPropertyName("watchlistId")]
        public int? WatchlistId { get; set; }
    }
}
