// SineUyum.Api/Dtos/UpdateProfileDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class UpdateProfileDto
    {
        [MaxLength(500)]
        public string? Bio { get; set; }

        // Yalnızca mevcut değer (değişmedi) ya da boş (kaldır) kabul edilir;
        // yeni fotoğraf POST /api/v1/profile/photo ile yüklenir. Mevcut değer
        // göreli bir yol (/media/...) olabileceği için [Url] kullanılmaz.
        [MaxLength(500)]
        public string? ProfileImageUrl { get; set; }
    }
}