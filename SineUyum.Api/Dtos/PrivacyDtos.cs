using System.ComponentModel.DataAnnotations;
using SineUyum.Api.Models;

namespace SineUyum.Api.Dtos
{
    // GET/PUT /api/v1/account/privacy. Enum'lar metin olarak taşınır
    // ("Public" | "Following" | "Private", "Everyone" | "Following" | "Nobody").
    public class PrivacySettingsDto
    {
        [Required]
        [EnumDataType(typeof(ProfileVisibility))]
        public ProfileVisibility? ProfileVisibility { get; set; }

        [Required]
        [EnumDataType(typeof(MessagePermission))]
        public MessagePermission? MessagePermission { get; set; }

        public bool HiddenFromSearch { get; set; }
    }

    // GET /api/v1/blocks
    public class BlockedUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime BlockedAt { get; set; }
    }
}
