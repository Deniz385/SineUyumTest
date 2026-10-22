using System.ComponentModel.DataAnnotations;
using SineUyum.Api.Models;

namespace SineUyum.Api.Dtos
{
    // GET/PUT /api/v1/account/privacy. Enums travel as text
    // ("Public" | "Following" | "Private", "Everyone" | "Following" | "Nobody").
    public class PrivacySettingsDto
    {
        [Required(ErrorMessage = "validation.required")]
        [EnumDataType(typeof(ProfileVisibility))]
        public ProfileVisibility? ProfileVisibility { get; set; }

        [Required(ErrorMessage = "validation.required")]
        [EnumDataType(typeof(MessagePermission))]
        public MessagePermission? MessagePermission { get; set; }

        public bool HiddenFromSearch { get; set; }

        // Read receipts (M10). Unchanged when omitted in a PUT: old clients that do
        // not know this field must not turn the setting off by accident.
        public bool? ShowReadReceipts { get; set; }
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
