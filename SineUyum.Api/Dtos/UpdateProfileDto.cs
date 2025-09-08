// SineUyum.Api/Dtos/UpdateProfileDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class UpdateProfileDto
    {
        [MaxLength(500)]
        public string? Bio { get; set; }

        [Url]
        public string? ProfileImageUrl { get; set; }
    }
}