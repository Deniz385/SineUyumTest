// SineUyum.Api/Dtos/CreateMessageDto.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization; // <-- BU SATIRI EKLEYİN

namespace SineUyum.Api.Dtos
{
    public class CreateMessageDto
    {
        [Required]
        public string RecipientId { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Content { get; set; }

        // --- DEĞİŞİKLİK BURADA ---
        [JsonPropertyName("movieId")] // Gelen JSON'daki "movieId" alanını bu özelliğe bağla
        public int? MovieId { get; set; }
        [JsonPropertyName("watchlistId")]
        public int? WatchlistId { get; set; }
    }
}