using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    // ErrorMessage values are locale keys (localized via AddDataAnnotationsLocalization).
    public class RegisterDto
    {
        [Required(ErrorMessage = "validation.usernameRequired")]
        [MinLength(3, ErrorMessage = "validation.usernameMinLength")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "validation.emailRequired")]
        [EmailAddress(ErrorMessage = "validation.emailInvalid")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "validation.passwordRequired")]
        [MinLength(6, ErrorMessage = "validation.passwordMinLength")]
        public string Password { get; set; } = string.Empty;
    }
}
