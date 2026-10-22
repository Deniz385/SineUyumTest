using SineUyum.Api.Dtos;
using SineUyum.Api.Localization;
using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// The rules of the compatibility explanation (pure computation).
public class CompatibilityInsightsTests
{
    private static readonly JsonStringLocalizer En = new(LocaleCatalog.Shared, "en");
    private static readonly JsonStringLocalizer Tr = new(LocaleCatalog.Shared, "tr");

    private static CommonMovieDto M(int id, int mine, int theirs) =>
        new() { MovieId = id, Title = $"Movie {id}", CurrentUserRating = mine, TargetUserRating = theirs };

    // The name is the stored one; known TMDb ids are shown by their locale name.
    private static GenreTaste G(int id, string storedName, double avg, int count = 3) => new(id, storedName, avg, count);

    private static readonly GenreTaste[] Mine = [G(18, "Drama", 9), G(878, "Science Fiction", 8), G(27, "Horror", 9), G(35, "Comedy", 5)];
    private static readonly GenreTaste[] Theirs = [G(18, "Drama", 8.5), G(878, "Science Fiction", 9), G(27, "Horror", 3), G(35, "Comedy", 6)];

    [Fact]
    public void SharedAndDivergentGenres_InOneSentence()
    {
        var result = CompatibilityInsights.Build([], Mine, Theirs, En);

        Assert.Equal(["Drama", "Science Fiction"], result.SharedGenres);
        Assert.Equal(["Horror"], result.DivergentGenres);
        Assert.Equal("You both love Drama and Science Fiction movies, but you differ on Horror movies.", result.Summary);
    }

    // Turkish: genre names come from the locale by TMDb id, whatever name is stored.
    [Fact]
    public void Turkish_SummaryAndGenreNames()
    {
        var result = CompatibilityInsights.Build([], Mine, Theirs, Tr);

        Assert.Equal(["Dram", "Bilim-Kurgu"], result.SharedGenres);
        Assert.Equal(["Korku"], result.DivergentGenres);
        Assert.Equal("İkiniz de Dram ve Bilim-Kurgu filmlerini seviyorsunuz; Korku filmlerinde ayrışıyorsunuz.", result.Summary);
    }

    [Fact]
    public void DivergenceAlone_AndNullWhenNothingToSay()
    {
        var onlySplit = CompatibilityInsights.Build([], [G(27, "Horror", 9)], [G(27, "Horror", 2)], En);
        Assert.Equal("You differ on Horror movies.", onlySplit.Summary);
        Assert.Equal("Korku filmlerinde ayrışıyorsunuz.",
            CompatibilityInsights.Build([], [G(27, "Horror", 9)], [G(27, "Horror", 2)], Tr).Summary);

        var nothing = CompatibilityInsights.Build([], [G(35, "Comedy", 6)], [G(35, "Comedy", 6.5)], En);
        Assert.Null(nothing.Summary);
        Assert.Empty(nothing.SharedGenres);
        Assert.Empty(nothing.DivergentGenres);
    }

    // A genre id missing from the locale files keeps its stored name.
    [Fact]
    public void UnknownGenreId_FallsBackToStoredName()
    {
        var result = CompatibilityInsights.Build([], [G(424242, "Custom Genre", 9)], [G(424242, "Custom Genre", 8)], Tr);
        Assert.Equal(["Custom Genre"], result.SharedGenres);
        Assert.Equal("İkiniz de Custom Genre filmlerini seviyorsunuz.", result.Summary);
    }

    [Fact]
    public void NoGenreVerdictFromTooFewMovies()
    {
        // A single movie does not make "you both love horror".
        var result = CompatibilityInsights.Build([], [G(27, "Horror", 10, count: 1)], [G(27, "Horror", 10)], En);
        Assert.Empty(result.SharedGenres);
        Assert.Null(result.Summary);
    }

    [Fact]
    public void DebateMovie_BiggestDifference_NoneBelowThreshold()
    {
        var common = new[] { M(1, 10, 3), M(2, 9, 2), M(3, 7, 4), M(4, 2, 9) };
        var debate = CompatibilityInsights.Build(common, [], [], En).DebateMovie;
        // Movies 1, 2 and 4 differ by 7; ties go to the higher extreme rating, then the id.
        Assert.Equal(1, debate!.MovieId);

        Assert.Null(CompatibilityInsights.Build([M(5, 8, 5)], [], [], En).DebateMovie);
    }

    [Fact]
    public void SharedFavorite_BothRatedHighly()
    {
        var common = new[] { M(1, 10, 8), M(2, 9, 9), M(3, 10, 6) };
        Assert.Equal(2, CompatibilityInsights.Build(common, [], [], En).SharedFavorite!.MovieId);
        Assert.Null(CompatibilityInsights.Build([M(3, 10, 6)], [], [], En).SharedFavorite);
    }
}
