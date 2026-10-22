using System.Text.Json.Serialization;

namespace SineUyum.Api.Models
{
    // Who can see profile details (ratings, statistics, follow lists,
    // compatibility). Name, photo and bio are always visible so the user knows
    // whom they blocked / are writing to.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProfileVisibility
    {
        Public,
        // Only people the profile owner follows. Following needs no approval, so a
        // "my followers" option would protect nothing; control stays with the owner.
        Following,
        Private
    }

    // Who can send new messages.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessagePermission
    {
        Everyone,
        Following,
        Nobody
    }

    // A block is stored one way but works both ways: the two cannot see each
    // other's profiles, message or follow each other, and never meet in search
    // or event matching.
    public class UserBlock
    {
        public string BlockerId { get; set; } = string.Empty;
        public AppUser Blocker { get; set; } = null!;

        public string BlockedId { get; set; } = string.Empty;
        public AppUser Blocked { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
