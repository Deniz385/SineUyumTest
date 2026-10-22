namespace SineUyum.Api.Dtos
{
    // Dtos/CreateRatingDto.cs
    using System.ComponentModel.DataAnnotations;

    public class CreateRatingDto
    {
        [Required]
        public int MovieId { get; set; }

        [Required]
        [Range(1, 10, ErrorMessage = "validation.range")] // the rating must be between 1 and 10
        public int Rating { get; set; }
    }
}
