using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class RegisterDeviceDto
    {
        [Required]
        [MaxLength(512)]
        public string Token { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(android|ios)$", ErrorMessage = "Platform 'android' ya da 'ios' olmalı.")]
        public string Platform { get; set; } = string.Empty;
    }

    public class UnregisterDeviceDto
    {
        [Required]
        [MaxLength(512)]
        public string Token { get; set; } = string.Empty;
    }
}
