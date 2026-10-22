using SineUyum.Api.Localization;
using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// The rules of the film identity card (pure computation). Most assertions use
// Turkish to prove the Turkish texts and number formatting ("3,4").
public class FilmIdentityTests
{
    private static readonly JsonStringLocalizer En = new(LocaleCatalog.Shared, "en");
    private static readonly JsonStringLocalizer Tr = new(LocaleCatalog.Shared, "tr");

    private static int _id;
    private static RatedMovieFacts R(int rating, double? tmdb = null, params string[] genres) =>
        new(++_id, $"Movie {_id}", null, rating, tmdb, genres);

    [Fact]
    public void FewRatings_NewExplorer()
    {
        var id = FilmIdentity.Build("alice", [R(9), R(7)], Tr);
        Assert.Equal("Yeni Keşifçi", id.Title);
        Assert.Equal("Henüz 2 film puanladın; zevkin şekilleniyor.", id.TitleDescription);

        var en = FilmIdentity.Build("alice", [R(9), R(7)], En);
        Assert.Equal("New Explorer", en.Title);
        Assert.Equal("Just getting started with 2 rated so far; your taste is taking shape.", en.TitleDescription);
    }

    [Fact]
    public void DepartingFromConsensus_ContrarianCritic_AndMostContrarianRating()
    {
        var ratings = Enumerable.Range(0, 7).Select(_ => R(5, 8.0)).ToList(); // each 3 points off
        var take = R(2, 8.4);                                                 // 6.4 points off
        ratings.Add(take);
        var id = FilmIdentity.Build("alice", ratings, Tr);

        Assert.Equal("Aykırı Eleştirmen", id.Title);
        Assert.Equal(3.4, id.Deviation);
        Assert.Equal("Puanların genel kanıdan ortalama 3,4 puan sapıyor.", id.TitleDescription);
        Assert.Equal(take.MovieId, id.ContrarianTake!.MovieId);
        Assert.Equal(8.4, id.ContrarianTake.TmdbAverage);

        // Numbers follow the language's culture.
        var en = FilmIdentity.Build("alice", ratings, En);
        Assert.Equal("Contrarian Critic", en.Title);
        Assert.Equal("Your ratings differ from the consensus by 3.4 points on average.", en.TitleDescription);
    }

    [Fact]
    public void LittleTmdbData_NoDeviation_SmallDifferenceIsNotContrarian()
    {
        var id = FilmIdentity.Build("alice", [R(7, 7.5), R(8, 7.0), R(6, 5.0), R(7), R(7)], En);
        Assert.Null(id.Deviation);
        Assert.Null(id.ContrarianTake);
    }

    [Theory]
    [InlineData(9, "Cömert Seyirci", "Ortalaman 9,0: iyi filmi görünce saklamıyorsun.")]
    [InlineData(4, "Zor Beğenen", "Ortalaman 4,0: senden 10 almak kolay değil.")]
    public void TitleByAverage(int rating, string title, string description)
    {
        var id = FilmIdentity.Build("alice", Enumerable.Range(0, 6).Select(_ => R(rating)).ToList(), Tr);
        Assert.Equal(title, id.Title);
        Assert.Equal(description, id.TitleDescription);
    }

    [Fact]
    public void DominantGenre_Fan_AndGenreShares()
    {
        var ratings = new[]
        {
            R(7, null, "Korku"), R(8, null, "Korku"), R(6, null, "Korku", "Gerilim"),
            R(7, null, "Dram"), R(7, null, "Dram", "Gerilim"), R(7)
        };
        var id = FilmIdentity.Build("alice", ratings, Tr);

        Assert.Equal("Korku Tutkunu", id.Title);
        // Phrased so the Turkish percentage needs no number-dependent suffix.
        Assert.Equal("Puanladığın filmlerde Korku oranı: %60.", id.TitleDescription);
        Assert.Equal(["Korku", "Dram", "Gerilim"], id.TopGenres.Select(g => g.Name));
        Assert.Equal(7.0, id.TopGenres[0].Average);
        Assert.Equal(3, id.TopGenres[0].Count);

        var enFan = FilmIdentity.Build("alice", ratings.Select(r => r with { Genres = r.Genres.Select(g => g == "Korku" ? "Horror" : g).ToList() }).ToList(), En);
        Assert.Equal("Horror Fan", enFan.Title);
        Assert.Equal("60% of the movies you've rated are Horror.", enFan.TitleDescription);
    }

    [Fact]
    public void ManyGenres_Eclectic_BalancedIsExplorer()
    {
        string[] genres = ["A", "B", "C", "D", "E", "F"];
        var many = genres.SelectMany(g => new[] { R(7, null, g), R(7, null, g) }).ToList();
        Assert.Equal("Her Telden", FilmIdentity.Build("x", many, Tr).Title);
        Assert.Equal("Eclectic Viewer", FilmIdentity.Build("x", many, En).Title);

        var balanced = new[] { R(7, null, "A"), R(7, null, "A"), R(7, null, "B"), R(7, null, "B"), R(7, null, "C"), R(7, null, "C") };
        Assert.Equal("Film Kâşifi", FilmIdentity.Build("x", balanced, Tr).Title);
        Assert.Equal("Film Explorer", FilmIdentity.Build("x", balanced, En).Title);
    }

    [Fact]
    public void Favorite_HighestRating()
    {
        var best = R(10);
        var id = FilmIdentity.Build("x", [R(8), best, R(9)], En);
        Assert.Equal(best.MovieId, id.Favorite!.MovieId);
        Assert.Null(FilmIdentity.Build("x", [], En).Favorite);
    }
}
