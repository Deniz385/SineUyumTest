namespace SineUyum.Api.Services
{
    // Ağırlıklı uyumluluk skoru daha önce AccountController, CompatibilityController
    // ve MatchingService içinde üç ayrı kopya halindeydi. Tek kaynağa alındı ki
    // algoritma değiştiğinde üç yer birbirinden sapmasın.
    public static class CompatibilityCalculator
    {
        // Ortak puan çiftlerinden 0-100 arası uyumluluk skoru üretir.
        // Ortak film yoksa 0 döner.
        public static double Score(IReadOnlyCollection<(int First, int Second)> commonRatings)
        {
            if (commonRatings.Count == 0) return 0;

            double totalWeightedScore = 0;
            foreach (var (first, second) in commonRatings)
            {
                double difference = Math.Abs(first - second);

                // İki taraf da çok sevdiyse ya da çok sevmediyse uyum daha anlamlı;
                // biri çok sevip diğeri nefret ettiyse uyumsuzluk daha ağır tartılır.
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

        // Film id -> puan sözlüklerinden ortak filmleri bulup skoru hesaplar.
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
