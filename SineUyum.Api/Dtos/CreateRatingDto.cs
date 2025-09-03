namespace SineUyum.Api.Dtos
{
    // Dtos/CreateRatingDto.cs
    using System.ComponentModel.DataAnnotations;

    public class CreateRatingDto
    {
        [Required]
        public int MovieId { get; set; }

        [Required]
        [Range(1, 10)] // Puanın 1 ile 10 arasında olmasını zorunlu kılar
        public int Rating { get; set; }
    }
}
