namespace SineUyum.Api.Dtos
{
    // GET /api/v1/watchlist listesindeki özet.
    public class WatchlistSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ItemCount { get; set; }
    }

    // Kendi listemizin detayı; oluşturma yanıtı da bu şekildedir (Location
    // başlığı zaten bu uca işaret eder).
    public class WatchlistDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<WatchlistItemDto> Items { get; set; } = new();
    }

    // Başkasının listesi: sahibinin adı da gösterilir.
    public class PublicWatchlistDto : WatchlistDetailDto
    {
        public string? OwnerUsername { get; set; }
    }

    public class WatchlistItemDto
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
