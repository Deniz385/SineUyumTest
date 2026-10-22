namespace SineUyum.Api.Models
{
    // A movie suggested to a group (M8). It used to be kept in
    // EventGroup.SuggestedMovieIds as "1,2,3" text: not queryable, no FK, and bad
    // data took the status screen down with a FormatException (500).
    public class EventGroupSuggestion
    {
        public int EventGroupId { get; set; }
        public EventGroup EventGroup { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        // Suggestion order (0 = strongest suggestion); shown in this order.
        public int Rank { get; set; }
    }
}
