using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;

namespace SineUyum.Api.Localization
{
    // A movie as a stored message argument (e.g. "X liked your review of
    // {movie}"). The id is kept so the title is rendered in the reader's
    // language; Title (the title at the time) is the fallback.
    public sealed record MovieRef(int MovieId, string Title)
    {
        public override string ToString() => Title;
    }

    public static class MovieTitles
    {
        // Replaces MovieRef arguments with the title in the given language.
        public static async Task<IReadOnlyList<object?[]>> LocalizeAsync(ApplicationDbContext db, IReadOnlyList<object?[]> argLists, string language)
        {
            var ids = argLists.SelectMany(a => a).OfType<MovieRef>().Select(m => m.MovieId).Distinct().ToList();
            if (ids.Count == 0) return argLists;

            var titles = await db.MovieTranslations
                .Where(t => ids.Contains(t.MovieId) && t.Language == language)
                .ToDictionaryAsync(t => t.MovieId, t => t.Title);

            return argLists
                .Select(args => args
                    .Select(a => a is MovieRef m && titles.TryGetValue(m.MovieId, out var title) ? m with { Title = title } : a)
                    .ToArray())
                .ToList();
        }
    }
}
