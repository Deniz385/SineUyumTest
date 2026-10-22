using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class LoginDto
    {
        [Required(ErrorMessage = "validation.usernameRequired")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "validation.passwordRequired")]
        public string Password { get; set; } = string.Empty;
    }
}
