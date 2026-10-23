using System.ComponentModel.DataAnnotations;
using SineUyum.Api.Models;

namespace SineUyum.Api.Dtos
{
    public class CreateWatchlistDto
    {
        [Required(ErrorMessage = "validation.required")]
        [MaxLength(100, ErrorMessage = "validation.maxLength")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "validation.maxLength")]
        public string? Description { get; set; }

        // "Private" | "Public". Create: Private when omitted; update: unchanged
        // when omitted (older clients do not send it).
        [EnumDataType(typeof(WatchlistVisibility))]
        public WatchlistVisibility? Visibility { get; set; }
    }
}
