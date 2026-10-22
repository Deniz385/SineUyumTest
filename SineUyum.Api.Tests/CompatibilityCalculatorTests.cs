using SineUyum.Api.Services;

namespace SineUyum.Api.Tests;

// The compatibility algorithm is the app's most critical business logic and is
// used in three places; these tests pin down its behaviour.
public class CompatibilityCalculatorTests
{
    [Fact]
    public void NoCommonMovies_ReturnsZero()
    {
        var result = CompatibilityCalculator.Score(Array.Empty<(int, int)>());
        Assert.Equal(0, result);
    }

    [Fact]
    public void IdenticalRatings_ReturnNearlyOneHundred()
    {
        // Difference 0 → full marks per movie. Weight is 1.5 since both are >= 8.
        // totalWeighted = (10 * 1.5) * 2 = 30, maxPossible = 2 * 10 * 1.5 = 30 → 100%
        var pairs = new (int, int)[] { (9, 9), (8, 8) };
        var result = CompatibilityCalculator.Score(pairs);
        Assert.Equal(100, result);
    }

    [Fact]
    public void OppositeRatings_GiveALowScore()
    {
        // (10,1) and (1,10): one loved it, the other hated it → weight 2.0,
        // difference 9 → movie score (10-9)=1. For two pairs totalWeighted = 1*2 + 1*2 = 4,
        // maxPossible = 2 * 10 * 1.5 = 30 → 4/30*100 ≈ 13.3
        var pairs = new (int, int)[] { (10, 1), (1, 10) };
        var result = CompatibilityCalculator.Score(pairs);
        Assert.True(result < 20, $"The score should be low for opposite ratings, got: {result}");
    }

    [Fact]
    public void Score_IsAlwaysBetweenZeroAndOneHundred()
    {
        // Even extreme values must not go outside 0-100.
        var pairs = new (int, int)[] { (1, 10), (1, 10), (2, 9), (1, 10) };
        var result = CompatibilityCalculator.Score(pairs);
        Assert.InRange(result, 0, 100);
    }

    [Fact]
    public void HighCompatibility_IsGreaterThanLowCompatibility()
    {
        var high = CompatibilityCalculator.Score(new (int, int)[] { (9, 9), (8, 9) });
        var low = CompatibilityCalculator.Score(new (int, int)[] { (9, 2), (8, 3) });
        Assert.True(high > low);
    }

    [Fact]
    public void DictionaryOverload_OnlyCountsCommonMovies()
    {
        // User 1: 1,2,3 — User 2: 2,3,4 — common: 2 and 3
        var r1 = new Dictionary<int, int> { [1] = 5, [2] = 9, [3] = 9 };
        var r2 = new Dictionary<int, int> { [2] = 9, [3] = 9, [4] = 1 };

        var dictionaryScore = CompatibilityCalculator.Score(r1, r2);
        // Common movies (9,9) and (9,9) → identical, 100% expected.
        var expected = CompatibilityCalculator.Score(new (int, int)[] { (9, 9), (9, 9) });

        Assert.Equal(expected, dictionaryScore);
    }

    [Fact]
    public void NoCommonMoviesInDictionaries_ReturnsZero()
    {
        var r1 = new Dictionary<int, int> { [1] = 8 };
        var r2 = new Dictionary<int, int> { [2] = 8 };
        Assert.Equal(0, CompatibilityCalculator.Score(r1, r2));
    }

    // IMPORTANT ALGORITHM PROPERTY: the score is normalized by the maximum
    // weight of 1.5. The weight is 1.5 only when both loved (>=8) or both
    // disliked (<=3) a movie; in the middle band (4-7) it stays 1.0. As a result
    // identical ratings give 100% only in the extreme bands; two users agreeing
    // perfectly in the middle cannot exceed 66.7%. This is the code's current,
    // possibly unintended behaviour — the tests pin it down (see roadmap,
    // improvement candidate).
    [Theory]
    [InlineData(10, 10, 100)]  // both >=8 → weight 1.5 → full marks
    [InlineData(1, 1, 100)]    // both <=3 → weight 1.5 → full marks
    [InlineData(5, 5, 66.66666666666667)]  // middle band → weight 1.0
    [InlineData(7, 7, 66.66666666666667)]
    public void SameRating_ScoresByBand(int a, int b, double expected)
    {
        var result = CompatibilityCalculator.Score(new (int, int)[] { (a, b) });
        Assert.Equal(expected, result, precision: 10);
    }
}
