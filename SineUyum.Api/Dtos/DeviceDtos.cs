using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class RegisterDeviceDto
    {
        [Required(ErrorMessage = "validation.required")]
        [MaxLength(512, ErrorMessage = "validation.maxLength")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "validation.required")]
        [RegularExpression("^(android|ios)$", ErrorMessage = "validation.platformInvalid")]
        public string Platform { get; set; } = string.Empty;
    }

    public class UnregisterDeviceDto
    {
        [Required(ErrorMessage = "validation.required")]
        [MaxLength(512, ErrorMessage = "validation.maxLength")]
        public string Token { get; set; } = string.Empty;
    }
}
