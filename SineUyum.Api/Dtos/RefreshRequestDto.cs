using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class RefreshRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
