using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [MinLength(3, ErrorMessage = "Kullanıcı adı en az 3 karakter olmalıdır.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        public string Password { get; set; } = string.Empty;
    }
}
