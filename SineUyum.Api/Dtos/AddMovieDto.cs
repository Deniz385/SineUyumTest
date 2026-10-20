using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SineUyum.Api.Dtos
{
    // İstemciler bu uca tarihsel olarak {id, title, posterPath} gönderiyor.
    // Yalnızca id okunur; başlık/poster TMDb'den çekildiği için yok sayılır.
    public class AddMovieDto
    {
        [Required]
        [JsonPropertyName("id")]
        public int MovieId { get; set; }
    }
}
