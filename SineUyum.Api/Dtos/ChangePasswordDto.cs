using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "validation.currentPasswordRequired")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "validation.newPasswordRequired")]
        [MinLength(6, ErrorMessage = "validation.newPasswordMinLength")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
