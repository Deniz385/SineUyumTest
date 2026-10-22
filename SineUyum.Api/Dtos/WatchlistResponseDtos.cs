namespace SineUyum.Api.Dtos
{
    // A summary in the GET /api/v1/watchlist list.
    public class WatchlistSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ItemCount { get; set; }
        // Shared list: it has at least one accepted member, or I am a member.
        public bool IsShared { get; set; }
        public bool IsOwner { get; set; } = true;
        public string? OwnerUsername { get; set; }
        // Number of accepted people, owner included.
        public int MemberCount { get; set; } = 1;
    }

    // Details of one of our lists; the create response has this shape too (the
    // Location header already points to this endpoint).
    public class WatchlistDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<WatchlistItemDto> Items { get; set; } = new();
        public bool IsOwner { get; set; } = true;
        public UserBriefWithImageDto? Owner { get; set; }
        // Members excluding the owner; pending invitations are visible only to the owner.
        public List<WatchlistMemberDto> Members { get; set; } = new();
    }

    // A (someone else's) list shared in a message: the owner's name is shown too.
    // Membership fields are deliberately absent (a separate class, not inheritance).
    public class PublicWatchlistDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<WatchlistItemDto> Items { get; set; } = new();
        public string? OwnerUsername { get; set; }
    }

    public class WatchlistItemDto
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public DateTime AddedAt { get; set; }
        // Who added it to a shared list (null in older rows).
        public string? AddedByUsername { get; set; }
        // The list's people (owner + members) who rated this movie.
        public List<string> WatchedBy { get; set; } = new();
    }

    public class WatchlistMemberDto
    {
        public UserBriefWithImageDto User { get; set; } = new();
        public bool Accepted { get; set; }
    }

    // GET /api/v1/watchlist/invitations
    public class WatchlistInvitationDto
    {
        public int ListId { get; set; }
        public string Name { get; set; } = string.Empty;
        public UserBriefWithImageDto Owner { get; set; } = new();
        public int ItemCount { get; set; }
        public DateTime InvitedAt { get; set; }
    }

    public class InviteToWatchlistDto
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "validation.required")]
        public string UserId { get; set; } = string.Empty;
    }
}
