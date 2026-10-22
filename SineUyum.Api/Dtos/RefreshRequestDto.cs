using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class RefreshRequestDto
    {
        [Required(ErrorMessage = "validation.required")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
