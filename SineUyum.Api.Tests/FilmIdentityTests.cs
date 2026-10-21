using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// Film kimliği kartının kuralları (saf hesap).
public class FilmIdentityTests
{
    private static int _id;
    private static RatedMovieFacts R(int rating, double? tmdb = null, params string[] genres) =>
        new(++_id, $"Film {_id}", null, rating, tmdb, genres);

    [Fact]
    public void AzPuanda_YeniKesifci()
    {
        var id = FilmIdentity.Build("ayse", [R(9), R(7)]);
        Assert.Equal("Yeni Keşifçi", id.Title);
        Assert.Equal("Henüz 2 film puanladın; zevkin şekilleniyor.", id.TitleDescription);
    }

    [Fact]
    public void GenelKanidanSapan_AykiriElestirmen_VeEnAykiriPuan()
    {
        var ratings = Enumerable.Range(0, 7).Select(_ => R(5, 8.0)).ToList(); // her biri 3 sapma
        var take = R(2, 8.4);                                                 // 6.4 sapma
        ratings.Add(take);
        var id = FilmIdentity.Build("ayse", ratings);

        Assert.Equal("Aykırı Eleştirmen", id.Title);
        Assert.Equal(3.4, id.Deviation);
        Assert.Equal("Puanların genel kanıdan ortalama 3,4 puan sapıyor.", id.TitleDescription);
        Assert.Equal(take.MovieId, id.ContrarianTake!.MovieId);
        Assert.Equal(8.4, id.ContrarianTake.TmdbAverage);
    }

    [Fact]
    public void AzTmdbVerisinde_SapmaHesaplanmaz_KucukFarkAykiriSayilmaz()
    {
        var id = FilmIdentity.Build("ayse", [R(7, 7.5), R(8, 7.0), R(6, 5.0), R(7), R(7)]);
        Assert.Null(id.Deviation);
        Assert.Null(id.ContrarianTake);
    }

    [Theory]
    [InlineData(9, "Cömert Seyirci", "Ortalaman 9,0: iyi filmi görünce saklamıyorsun.")]
    [InlineData(4, "Zor Beğenen", "Ortalaman 4,0: senden 10 almak kolay değil.")]
    public void Ortalamaya_GoreUnvan(int rating, string title, string description)
    {
        var id = FilmIdentity.Build("ayse", Enumerable.Range(0, 6).Select(_ => R(rating)).ToList());
        Assert.Equal(title, id.Title);
        Assert.Equal(description, id.TitleDescription);
    }

    [Fact]
    public void BaskinTur_Tutkunu_VeTurPaylari()
    {
        var ratings = new[]
        {
            R(7, null, "Korku"), R(8, null, "Korku"), R(6, null, "Korku", "Gerilim"),
            R(7, null, "Dram"), R(7, null, "Dram", "Gerilim"), R(7)
        };
        var id = FilmIdentity.Build("ayse", ratings);

        Assert.Equal("Korku Tutkunu", id.Title);
        Assert.Equal("Puanladığın filmlerin %60'i Korku.", id.TitleDescription);
        Assert.Equal(["Korku", "Dram", "Gerilim"], id.TopGenres.Select(g => g.Name));
        Assert.Equal(7.0, id.TopGenres[0].Average);
        Assert.Equal(3, id.TopGenres[0].Count);
    }

    [Fact]
    public void BirCokTur_HerTelden_DengeliyseKasif()
    {
        string[] genres = ["A", "B", "C", "D", "E", "F"];
        var many = genres.SelectMany(g => new[] { R(7, null, g), R(7, null, g) }).ToList();
        Assert.Equal("Her Telden", FilmIdentity.Build("x", many).Title);

        var balanced = new[] { R(7, null, "A"), R(7, null, "A"), R(7, null, "B"), R(7, null, "B"), R(7, null, "C"), R(7, null, "C") };
        Assert.Equal("Film Kâşifi", FilmIdentity.Build("x", balanced).Title);
    }

    [Fact]
    public void Favori_EnYuksekPuan()
    {
        var best = R(10);
        var id = FilmIdentity.Build("x", [R(8), best, R(9)]);
        Assert.Equal(best.MovieId, id.Favorite!.MovieId);
        Assert.Null(FilmIdentity.Build("x", []).Favorite);
    }
}
