namespace SineUyum.Api.Dtos
{
    // GET /api/v1/people/suggestions: people worth following.
    public class PersonSuggestionDto
    {
        public UserBriefWithImageDto User { get; set; } = new();
        public string? Bio { get; set; }
        // Null for "popular" suggestions (too few common movies to compare).
        public double? Compatibility { get; set; }
        public int CommonMovieCount { get; set; }
        public int FollowerCount { get; set; }
    }

    public class PeopleSuggestionsDto
    {
        // "taste": by compatibility; "popular": most followed (the viewer has
        // too few ratings, or nobody similar was found yet).
        public string Basis { get; set; } = "taste";
        public List<PersonSuggestionDto> Items { get; set; } = new();
    }
}
