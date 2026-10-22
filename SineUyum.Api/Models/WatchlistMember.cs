namespace SineUyum.Api.Models
{
    // Ortak izleme listesinin üyesi. Sahip Watchlist.UserId'dir ve burada
    // tutulmaz; davet kabul edilene kadar üye listeyi göremez/düzenleyemez.
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
