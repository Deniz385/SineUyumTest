// SineUyum.Api/Dtos/CreateEventDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class CreateEventDto
    {
        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [MaxLength(200)]
        public string LocationName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Address { get; set; }

        [Required]
        [Range(2, 10)] // Grup boyutu en az 2, en fazla 10 olabilir
        public int GroupSize { get; set; }
    }
}