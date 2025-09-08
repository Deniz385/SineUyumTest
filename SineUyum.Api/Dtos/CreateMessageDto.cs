// SineUyum.Api/Dtos/CreateMessageDto.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    public class CreateMessageDto
    {
        [Required]
        public string RecipientId { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
}