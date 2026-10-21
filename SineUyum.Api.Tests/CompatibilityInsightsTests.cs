using SineUyum.Api.Dtos;
using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// Uyum açıklamasının kuralları (saf hesap).
public class CompatibilityInsightsTests
{
    private static CommonMovieDto M(int id, int mine, int theirs) =>
        new() { MovieId = id, Title = $"Film {id}", CurrentUserRating = mine, TargetUserRating = theirs };

    private static GenreTaste G(int id, string name, double avg, int count = 3) => new(id, name, avg, count);

    [Fact]
    public void OrtakVeAyrisanTurler_TekCumlede()
    {
        var mine = new[] { G(18, "Dram", 9), G(878, "Bilim-Kurgu", 8), G(27, "Korku", 9), G(35, "Komedi", 5) };
        var theirs = new[] { G(18, "Dram", 8.5), G(878, "Bilim-Kurgu", 9), G(27, "Korku", 3), G(35, "Komedi", 6) };

        var result = CompatibilityInsights.Build([], mine, theirs);

        Assert.Equal(["Dram", "Bilim-Kurgu"], result.SharedGenres);
        Assert.Equal(["Korku"], result.DivergentGenres);
        Assert.Equal("İkiniz de Dram ve Bilim-Kurgu filmlerini seviyorsunuz; Korku filmlerinde ayrışıyorsunuz.", result.Summary);
    }

    [Fact]
    public void TekBasinaAyrisma_VeHicBirSeyYoksaNull()
    {
        var onlySplit = CompatibilityInsights.Build([], [G(27, "Korku", 9)], [G(27, "Korku", 2)]);
        Assert.Equal("Korku filmlerinde ayrışıyorsunuz.", onlySplit.Summary);

        var nothing = CompatibilityInsights.Build([], [G(35, "Komedi", 6)], [G(35, "Komedi", 6.5)]);
        Assert.Null(nothing.Summary);
        Assert.Empty(nothing.SharedGenres);
        Assert.Empty(nothing.DivergentGenres);
    }

    [Fact]
    public void AzFilmeDayananTurYargisiYapilmaz()
    {
        // Tek filmle "ikiniz de korku seviyorsunuz" denmez.
        var result = CompatibilityInsights.Build([], [G(27, "Korku", 10, count: 1)], [G(27, "Korku", 10)]);
        Assert.Empty(result.SharedGenres);
        Assert.Null(result.Summary);
    }

    [Fact]
    public void TartismaFilmi_EnBuyukFark_EsikAltindaYok()
    {
        var common = new[] { M(1, 10, 3), M(2, 9, 2), M(3, 7, 4), M(4, 2, 9) };
        var debate = CompatibilityInsights.Build(common, [], []).DebateMovie;
        // 1 ve 2 ve 4'te fark 7; eşitlikte daha yüksek uç puan, sonra id.
        Assert.Equal(1, debate!.MovieId);

        Assert.Null(CompatibilityInsights.Build([M(5, 8, 5)], [], []).DebateMovie);
    }

    [Fact]
    public void OrtakFavori_IkisininDeYuksekPuani()
    {
        var common = new[] { M(1, 10, 8), M(2, 9, 9), M(3, 10, 6) };
        Assert.Equal(2, CompatibilityInsights.Build(common, [], []).SharedFavorite!.MovieId);
        Assert.Null(CompatibilityInsights.Build([M(3, 10, 6)], [], []).SharedFavorite);
    }
}
