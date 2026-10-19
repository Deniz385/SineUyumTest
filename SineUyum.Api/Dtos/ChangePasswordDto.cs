using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Yeni şifre en az 6 karakter olmalıdır.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
