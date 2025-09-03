namespace SineUyum.Api.Dtos
{
    // Dtos/RegisterDto.cs
    using System.ComponentModel.DataAnnotations;

    public class RegisterDto
    {
        [Required] // Bu alanın zorunlu olduğunu belirtir.
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress] // Geçerli bir e-posta formatında olmasını sağlar.
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)] // Şifrenin en az 6 karakter olmasını sağlar.
        public string Password { get; set; } = string.Empty;
    }
}
