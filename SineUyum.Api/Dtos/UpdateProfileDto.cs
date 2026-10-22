// SineUyum.Api/Dtos/UpdateProfileDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class UpdateProfileDto
    {
        [MaxLength(500, ErrorMessage = "validation.maxLength")]
        public string? Bio { get; set; }

        // Only the current value (unchanged) or empty (remove) is accepted; a new
        // photo is uploaded with POST /api/v1/profile/photo. The current value may
        // be a relative path (/media/...), so [Url] is not used.
        [MaxLength(500, ErrorMessage = "validation.maxLength")]
        public string? ProfileImageUrl { get; set; }
    }
}