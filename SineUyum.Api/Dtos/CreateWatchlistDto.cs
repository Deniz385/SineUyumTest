using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class CreateWatchlistDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}