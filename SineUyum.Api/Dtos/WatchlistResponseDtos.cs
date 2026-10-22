namespace SineUyum.Api.Dtos
{
    // GET /api/v1/watchlist listesindeki özet.
    public class WatchlistSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ItemCount { get; set; }
        // Ortak liste: kabul etmiş en az bir üyesi var ya da ben üyeyim.
        public bool IsShared { get; set; }
        public bool IsOwner { get; set; } = true;
        public string? OwnerUsername { get; set; }
        // Sahip dahil kabul etmiş kişi sayısı.
        public int MemberCount { get; set; } = 1;
    }

    // Kendi listemizin detayı; oluşturma yanıtı da bu şekildedir (Location
    // başlığı zaten bu uca işaret eder).
    public class WatchlistDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<WatchlistItemDto> Items { get; set; } = new();
        public bool IsOwner { get; set; } = true;
        public UserBriefWithImageDto? Owner { get; set; }
        // Sahip hariç üyeler; bekleyen davetler yalnızca sahibe görünür.
        public List<WatchlistMemberDto> Members { get; set; } = new();
    }

    // Mesajla paylaşılan (başkasının) liste: sahibinin adı da gösterilir.
    // Üyelik alanları bilerek yok (ayrı sınıf, kalıtım değil).
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
        // Ortak listede ekleyen (eski kayıtlarda null).
        public string? AddedByUsername { get; set; }
        // Liste kişilerinden (sahip + üyeler) bu filmi puanlamış olanlar.
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
        [System.ComponentModel.DataAnnotations.Required]
        public string UserId { get; set; } = string.Empty;
    }
}
