using Microsoft.Extensions.Localization;
using SineUyum.Api.Dtos;
using SineUyum.Api.Localization;

namespace SineUyum.Api.Services
{
    // A user's average rating in a genre (based on at least a few movies).
    // Name is the stored (TMDb) name, used only for genres without a locale entry.
    public record GenreTaste(int GenreId, string Name, double Average, int Count);

    // Explains the compatibility score: "why you match", "where you differ",
    // a shared favorite and a "debate movie" as a conversation starter. Pure
    // computation; knows nothing about the database (unit tests pin it down).
    // Texts and genre names come from the given localizer.
    public static class CompatibilityInsights
    {
        // At least this many ratings per person for a genre verdict.
        public const int MinMoviesPerGenre = 2;
        // Both averages above this: "you both love it".
        public const double LovedThreshold = 7.0;
        // This much difference between the averages is a "divergence".
        public const double DivergenceThreshold = 2.5;
        // Shared favorite: both gave at least this rating.
        public const int FavoriteThreshold = 8;
        // Debate movie: at least this much difference between the ratings.
        public const int DebateThreshold = 4;

        public static CompatibilityInsightsDto Build(
            IReadOnlyCollection<CommonMovieDto> commonMovies,
            IReadOnlyCollection<GenreTaste> mine,
            IReadOnlyCollection<GenreTaste> theirs,
            IStringLocalizer t)
        {
            var myGenres = mine.Where(g => g.Count >= MinMoviesPerGenre).ToDictionary(g => g.GenreId);
            var pairs = theirs
                .Where(g => g.Count >= MinMoviesPerGenre && myGenres.ContainsKey(g.GenreId))
                .Select(g => (Name: t.GenreName(g.GenreId, g.Name), Me: myGenres[g.GenreId].Average, Them: g.Average))
                .ToList();

            var shared = pairs
                .Where(p => p.Me >= LovedThreshold && p.Them >= LovedThreshold)
                .OrderByDescending(p => Math.Min(p.Me, p.Them))
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .Take(2)
                .Select(p => p.Name)
                .ToList();

            var divergent = pairs
                .Where(p => Math.Abs(p.Me - p.Them) >= DivergenceThreshold)
                .OrderByDescending(p => Math.Abs(p.Me - p.Them))
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .Take(2)
                .Select(p => p.Name)
                .ToList();

            var favorite = commonMovies
                .Where(m => m.CurrentUserRating >= FavoriteThreshold && m.TargetUserRating >= FavoriteThreshold)
                .OrderByDescending(m => Math.Min(m.CurrentUserRating, m.TargetUserRating))
                .ThenByDescending(m => m.CurrentUserRating + m.TargetUserRating)
                .ThenBy(m => m.MovieId)
                .FirstOrDefault();

            var debate = commonMovies
                .Where(m => Math.Abs(m.CurrentUserRating - m.TargetUserRating) >= DebateThreshold)
                .OrderByDescending(m => Math.Abs(m.CurrentUserRating - m.TargetUserRating))
                .ThenByDescending(m => Math.Max(m.CurrentUserRating, m.TargetUserRating))
                .ThenBy(m => m.MovieId)
                .FirstOrDefault();

            return new CompatibilityInsightsDto
            {
                Summary = Summarize(shared, divergent, t),
                SharedGenres = shared,
                DivergentGenres = divergent,
                SharedFavorite = favorite,
                DebateMovie = debate
            };
        }

        // Whole-sentence templates per case: word order and capitalization
        // differ between languages.
        private static string? Summarize(List<string> shared, List<string> divergent, IStringLocalizer t) =>
            (shared.Count > 0, divergent.Count > 0) switch
            {
                (true, true) => t["insights.summaryBoth", Join(shared, t), Join(divergent, t)].Value,
                (true, false) => t["insights.summaryShared", Join(shared, t)].Value,
                (false, true) => t["insights.summaryDivergent", Join(divergent, t)].Value,
                _ => null
            };

        private static string Join(List<string> names, IStringLocalizer t) =>
            names.Count == 1 ? names[0] : t["insights.genrePair", names[0], names[1]];
    }
}
