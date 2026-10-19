using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// Uyumluluk algoritması uygulamanın en kritik iş mantığı ve üç yerde
// kullanılıyor; davranışını bu testler sabitliyor.
public class CompatibilityCalculatorTests
{
    [Fact]
    public void OrtakFilmYoksa_SifirDoner()
    {
        var sonuc = CompatibilityCalculator.Score(Array.Empty<(int, int)>());
        Assert.Equal(0, sonuc);
    }

    [Fact]
    public void BirebirAyniPuanlar_YuzuzeyakinDoner()
    {
        // Fark 0 → her film tam puan. Ağırlık, her ikisi de >=8 olduğu için 1.5.
        // totalWeighted = (10 * 1.5) * 2 = 30, maxPossible = 2 * 10 * 1.5 = 30 → %100
        var pairs = new (int, int)[] { (9, 9), (8, 8) };
        var sonuc = CompatibilityCalculator.Score(pairs);
        Assert.Equal(100, sonuc);
    }

    [Fact]
    public void TamZitPuanlar_DusukSkorVerir()
    {
        // (10,1) ve (1,10): biri çok sevmiş diğeri nefret etmiş → ağırlık 2.0,
        // fark 9 → film skoru (10-9)=1. İki çift için totalWeighted = 1*2 + 1*2 = 4,
        // maxPossible = 2 * 10 * 1.5 = 30 → 4/30*100 ≈ 13.3
        var pairs = new (int, int)[] { (10, 1), (1, 10) };
        var sonuc = CompatibilityCalculator.Score(pairs);
        Assert.True(sonuc < 20, $"Zıt puanlarda skor düşük olmalı, bulundu: {sonuc}");
    }

    [Fact]
    public void Skor_HerZaman_SifirYuzArasinda()
    {
        // Uç değerlerin bile 0-100 dışına taşmadığını doğrula.
        var pairs = new (int, int)[] { (1, 10), (1, 10), (2, 9), (1, 10) };
        var sonuc = CompatibilityCalculator.Score(pairs);
        Assert.InRange(sonuc, 0, 100);
    }

    [Fact]
    public void YuksekUyum_DusukUyumdan_BuyukOlmali()
    {
        var yuksek = CompatibilityCalculator.Score(new (int, int)[] { (9, 9), (8, 9) });
        var dusuk = CompatibilityCalculator.Score(new (int, int)[] { (9, 2), (8, 3) });
        Assert.True(yuksek > dusuk);
    }

    [Fact]
    public void SozlukAsiriYuku_YalnizcaOrtakFilmleriHesaplar()
    {
        // Kullanıcı1: 1,2,3 — Kullanıcı2: 2,3,4 — ortak: 2 ve 3
        var r1 = new Dictionary<int, int> { [1] = 5, [2] = 9, [3] = 9 };
        var r2 = new Dictionary<int, int> { [2] = 9, [3] = 9, [4] = 1 };

        var sozlukSkoru = CompatibilityCalculator.Score(r1, r2);
        // Ortak filmler (9,9) ve (9,9) → birebir aynı, %100 beklenir.
        var beklenen = CompatibilityCalculator.Score(new (int, int)[] { (9, 9), (9, 9) });

        Assert.Equal(beklenen, sozlukSkoru);
    }

    [Fact]
    public void HicOrtakFilmYokSozlukte_SifirDoner()
    {
        var r1 = new Dictionary<int, int> { [1] = 8 };
        var r2 = new Dictionary<int, int> { [2] = 8 };
        Assert.Equal(0, CompatibilityCalculator.Score(r1, r2));
    }

    // ÖNEMLİ ALGORİTMA ÖZELLİĞİ: skor, maksimum ağırlık olan 1.5'e göre
    // normalize ediliyor. Ağırlık yalnızca iki taraf da çok sevdiğinde (≥8) ya
    // da çok sevmediğinde (≤3) 1.5 oluyor; orta bantta (4-7) 1.0 kalıyor. Sonuç:
    // birebir aynı puan ancak uç bantlarda %100 verir; ortada tam anlaşan iki
    // kullanıcı %66.7'yi geçemez. Bu, kodun mevcut ve bilinçli olmayabilecek
    // davranışı — testler sabitliyor (bkz. yol haritası, iyileştirme adayı).
    [Theory]
    [InlineData(10, 10, 100)]  // ikisi de ≥8 → ağırlık 1.5 → tam puan
    [InlineData(1, 1, 100)]    // ikisi de ≤3 → ağırlık 1.5 → tam puan
    [InlineData(5, 5, 66.66666666666667)]  // orta bant → ağırlık 1.0
    [InlineData(7, 7, 66.66666666666667)]
    public void AyniPuan_BandaGoreSkorVerir(int a, int b, double beklenen)
    {
        var sonuc = CompatibilityCalculator.Score(new (int, int)[] { (a, b) });
        Assert.Equal(beklenen, sonuc, precision: 10);
    }
}
