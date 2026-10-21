using SineUyum.Api.Dtos;

namespace SineUyum.Api.Services
{
    // Bir kullanıcının bir türdeki puan ortalaması (en az birkaç filme dayanır).
    public record GenreTaste(int GenreId, string Name, double Average, int Count);

    // Uyum puanını açıklar: "neden uyumlusunuz", "nerede ayrışıyorsunuz", ortak
    // favori ve sohbet başlatıcı olarak "tartışma filmi". Saf hesap; veritabanı
    // bilmez (birim testleri bunu sabitler).
    public static class CompatibilityInsights
    {
        // Tür yargısı için kişi başına en az bu kadar puan.
        public const int MinMoviesPerGenre = 2;
        // İkisinin de ortalaması bunun üstündeyse "ikiniz de seviyorsunuz".
        public const double LovedThreshold = 7.0;
        // Ortalamalar arası bu kadar fark "ayrışma".
        public const double DivergenceThreshold = 2.5;
        // Ortak favori: ikisi de en az bu puanı vermiş.
        public const int FavoriteThreshold = 8;
        // Tartışma filmi: puanlar arası en az bu kadar fark.
        public const int DebateThreshold = 4;

        public static CompatibilityInsightsDto Build(
            IReadOnlyCollection<CommonMovieDto> commonMovies,
            IReadOnlyCollection<GenreTaste> mine,
            IReadOnlyCollection<GenreTaste> theirs)
        {
            var myGenres = mine.Where(g => g.Count >= MinMoviesPerGenre).ToDictionary(g => g.GenreId);
            var pairs = theirs
                .Where(g => g.Count >= MinMoviesPerGenre && myGenres.ContainsKey(g.GenreId))
                .Select(t => (Name: t.Name, Me: myGenres[t.GenreId].Average, Them: t.Average))
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
                Summary = Summarize(shared, divergent),
                SharedGenres = shared,
                DivergentGenres = divergent,
                SharedFavorite = favorite,
                DebateMovie = debate
            };
        }

        private static string? Summarize(List<string> shared, List<string> divergent)
        {
            var loves = shared.Count > 0 ? $"İkiniz de {Join(shared)} filmlerini seviyorsunuz" : null;
            var splits = divergent.Count > 0 ? $"{Join(divergent)} filmlerinde ayrışıyorsunuz" : null;
            return (loves, splits) switch
            {
                (not null, not null) => $"{loves}; {splits}.",
                (not null, null) => $"{loves}.",
                // Tür adları zaten büyük harfle başlar.
                (null, not null) => $"{splits}.",
                _ => null
            };
        }

        private static string Join(List<string> names) =>
            names.Count == 1 ? names[0] : $"{names[0]} ve {names[1]}";
    }
}
