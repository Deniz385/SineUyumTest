namespace SineUyum.Api.Models
{
    // Bir gruba önerilen film (M8). Önceden EventGroup.SuggestedMovieIds'te
    // "1,2,3" metni olarak tutuluyordu: sorgulanamıyor, FK yoktu ve bozuk veri
    // durum ekranını FormatException ile 500'e düşürüyordu.
    public class EventGroupSuggestion
    {
        public int EventGroupId { get; set; }
        public EventGroup EventGroup { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        // Öneri sırası (0 = en güçlü öneri); ekranda bu sırayla gösterilir.
        public int Rank { get; set; }
    }
}
