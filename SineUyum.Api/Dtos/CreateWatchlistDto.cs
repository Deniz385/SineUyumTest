using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class CreateWatchlistDto
    {
        [Required(ErrorMessage = "validation.required")]
        [MaxLength(100, ErrorMessage = "validation.maxLength")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "validation.maxLength")]
        public string? Description { get; set; }
    }
}
