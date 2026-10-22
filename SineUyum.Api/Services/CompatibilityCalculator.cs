namespace SineUyum.Api.Services
{
    // The weighted compatibility score used to exist as three separate copies
    // in AccountController, CompatibilityController and MatchingService. It now
    // has a single source so the three places cannot drift when the algorithm changes.
    public static class CompatibilityCalculator
    {
        // Computes a 0-100 compatibility score from pairs of common ratings.
        // Returns 0 when there are no common movies.
        public static double Score(IReadOnlyCollection<(int First, int Second)> commonRatings)
        {
            if (commonRatings.Count == 0) return 0;

            double totalWeightedScore = 0;
            foreach (var (first, second) in commonRatings)
            {
                double difference = Math.Abs(first - second);

                // Agreement means more when both loved or both disliked a movie;
                // one loving and the other hating it weighs heavier as a mismatch.
                double weight = 1.0;
                if ((first <= 3 && second <= 3) || (first >= 8 && second >= 8))
                {
                    weight = 1.5;
                }
                else if ((first <= 3 && second >= 8) || (first >= 8 && second <= 3))
                {
                    weight = 2.0;
                }

                totalWeightedScore += (10 - difference) * weight;
            }

            double maxPossibleScore = commonRatings.Count * 10 * 1.5;
            if (maxPossibleScore <= 0) return 0;

            double score = (totalWeightedScore / maxPossibleScore) * 100;
            return Math.Max(0, Math.Min(100, score));
        }

        // Finds the common movies in two movie id -> rating dictionaries and computes the score.
        public static double Score(IReadOnlyDictionary<int, int> ratings1, IReadOnlyDictionary<int, int> ratings2)
        {
            var pairs = ratings1.Keys
                .Where(ratings2.ContainsKey)
                .Select(movieId => (ratings1[movieId], ratings2[movieId]))
                .ToList();

            return Score(pairs);
        }
    }
}
