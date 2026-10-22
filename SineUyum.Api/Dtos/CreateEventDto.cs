// SineUyum.Api/Dtos/CreateEventDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class CreateEventDto
    {
        [Required]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "validation.required")]
        [MaxLength(200, ErrorMessage = "validation.maxLength")]
        public string LocationName { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "validation.maxLength")]
        public string? Address { get; set; }

        [Required]
        [Range(2, 10, ErrorMessage = "validation.range")] // a group has 2 to 10 people
        public int GroupSize { get; set; }
    }
}
