using SineUyum.Api.Dtos;

namespace SineUyum.Api.Services
{
    public record RatedMovieFacts(int MovieId, string Title, string? PosterPath, int Rating, double? TmdbAverage, IReadOnlyList<string> Genres);

    // Film kimliği kartı: puanlardan çıkan zevk profili ve bir unvan. Saf hesap.
    public static class FilmIdentity
    {
        public const int MinForTitle = 5;
        public const int MinGenreMovies = 2;
        public const int MinForContrarian = 8;
        public const double ContrarianThreshold = 2.0;
        public const int TakeThreshold = 3;

        public static FilmIdentityDto Build(string userName, IReadOnlyCollection<RatedMovieFacts> ratings)
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

            var (title, description) = Archetype(total, average, deviation, genres);
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

        private static (string Title, string Description) Archetype(int total, double average, double? deviation, List<GenreShareDto> genres)
        {
            if (total < MinForTitle)
                return ("Yeni Keşifçi", $"Henüz {total} film puanladın; zevkin şekilleniyor.");
            if (deviation >= ContrarianThreshold)
                return ("Aykırı Eleştirmen", $"Puanların genel kanıdan ortalama {Format(deviation.Value)} puan sapıyor.");
            if (average >= 8.0)
                return ("Cömert Seyirci", $"Ortalaman {Format(average)}: iyi filmi görünce saklamıyorsun.");
            if (average <= 5.5)
                return ("Zor Beğenen", $"Ortalaman {Format(average)}: senden 10 almak kolay değil.");
            if (genres.Count > 0 && genres[0].Share >= 40)
                return ($"{genres[0].Name} Tutkunu", $"Puanladığın filmlerin %{genres[0].Share}'i {genres[0].Name}.");
            if (genres.Count >= 6)
                return ("Her Telden", $"{genres.Count} farklı türde düzenli film izliyorsun.");
            return ("Film Kâşifi", "Dengeli bir zevkin var; her türe açıksın.");
        }

        private static string Format(double value) => value.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
    }
}
