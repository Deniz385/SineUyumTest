// SineUyum.Api/Dtos/AddWatchlistItemDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class AddWatchlistItemDto
    {
        [Required]
        public int MovieId { get; set; }

        // Geriye dönük uyumluluk için kabul edilir ama YOK SAYILIR: film meta
        // verisi sunucuda TMDb'den çekilir (MovieCatalogService). Eski
        // istemciler göndermeye devam edebilir.
        public string? Title { get; set; }

        public string? PosterPath { get; set; }
    }
}
