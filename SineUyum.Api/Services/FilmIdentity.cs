using Microsoft.Extensions.Localization;
using SineUyum.Api.Dtos;

namespace SineUyum.Api.Services
{
    // Genres are display names, already localized by the caller.
    public record RatedMovieFacts(int MovieId, string Title, string? PosterPath, int Rating, double? TmdbAverage, IReadOnlyList<string> Genres);

    // The film identity card: the taste profile derived from ratings and a
    // title. Pure computation; texts come from the given localizer.
    public static class FilmIdentity
    {
        public const int MinForTitle = 5;
        public const int MinGenreMovies = 2;
        public const int MinForContrarian = 8;
        public const double ContrarianThreshold = 2.0;
        public const int TakeThreshold = 3;

        public static FilmIdentityDto Build(string userName, IReadOnlyCollection<RatedMovieFacts> ratings, IStringLocalizer t)
        {
            var total = ratings.Count;
            var average = total == 0 ? 0 : Math.Round(ratings.Average(r => r.Rating), 1);

            var withGenres = ratings.Where(r => r.Genres.Count > 0).ToList();
            var genres = withGenres
                .SelectMany(r => r.Genres.Select(g => (Genre: g, r.Rating)))
                .GroupBy(x => x.Genre)
                .Where(g => g.Count() >= MinGenreMovies)
                .Select(g => new GenreShareDto
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Average = Math.Round(g.Average(x => x.Rating), 1),
                    Share = (int)Math.Round(100.0 * g.Count() / withGenres.Count)
                })
                .OrderByDescending(g => g.Count).ThenByDescending(g => g.Average).ThenBy(g => g.Name, StringComparer.Ordinal)
                .ToList();

            var withTmdb = ratings.Where(r => r.TmdbAverage is > 0).ToList();
            double? deviation = withTmdb.Count >= MinForContrarian
                ? Math.Round(withTmdb.Average(r => Math.Abs(r.Rating - r.TmdbAverage!.Value)), 1)
                : null;

            var take = withTmdb
                .Where(r => Math.Abs(r.Rating - r.TmdbAverage!.Value) >= TakeThreshold)
                .OrderByDescending(r => Math.Abs(r.Rating - r.TmdbAverage!.Value))
                .ThenBy(r => r.MovieId)
                .FirstOrDefault();

            var favorite = ratings.OrderByDescending(r => r.Rating).ThenByDescending(r => r.MovieId).FirstOrDefault();

            var (title, description) = Archetype(total, average, deviation, genres, t);
            return new FilmIdentityDto
            {
                UserName = userName,
                TotalRatings = total,
                AverageRating = average,
                Title = title,
                TitleDescription = description,
                TopGenres = genres.Take(3).ToList(),
                Deviation = deviation,
                ContrarianTake = take == null ? null : new IdentityMovieDto
                {
                    MovieId = take.MovieId, Title = take.Title, PosterPath = take.PosterPath,
                    Rating = take.Rating, TmdbAverage = take.TmdbAverage
                },
                Favorite = favorite == null ? null : new IdentityMovieDto
                {
                    MovieId = favorite.MovieId, Title = favorite.Title, PosterPath = favorite.PosterPath,
                    Rating = favorite.Rating, TmdbAverage = favorite.TmdbAverage
                }
            };
        }

        // Numbers are formatted by the templates ({0:0.0}) in the language's culture.
        private static (string Title, string Description) Archetype(int total, double average, double? deviation, List<GenreShareDto> genres, IStringLocalizer t)
        {
            if (total < MinForTitle)
                return (t["filmIdentity.newExplorer.title"], t["filmIdentity.newExplorer.description", total]);
            if (deviation >= ContrarianThreshold)
                return (t["filmIdentity.contrarian.title"], t["filmIdentity.contrarian.description", deviation.Value]);
            if (average >= 8.0)
                return (t["filmIdentity.generous.title"], t["filmIdentity.generous.description", average]);
            if (average <= 5.5)
                return (t["filmIdentity.hardToPlease.title"], t["filmIdentity.hardToPlease.description", average]);
            if (genres.Count > 0 && genres[0].Share >= 40)
                return (t["filmIdentity.genreFan.title", genres[0].Name], t["filmIdentity.genreFan.description", genres[0].Name, genres[0].Share]);
            if (genres.Count >= 6)
                return (t["filmIdentity.eclectic.title"], t["filmIdentity.eclectic.description", genres.Count]);
            return (t["filmIdentity.explorer.title"], t["filmIdentity.explorer.description"]);
        }
    }
}
