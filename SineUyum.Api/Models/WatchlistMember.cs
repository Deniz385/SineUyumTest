namespace SineUyum.Api.Models
{
    // A member of a shared watchlist. The owner is Watchlist.UserId and is not
    // stored here; until the invitation is accepted the member cannot see/edit the list.
    public class WatchlistMember
    {
        public int WatchlistId { get; set; }
        public Watchlist Watchlist { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public bool Accepted { get; set; }
        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
    }
}
