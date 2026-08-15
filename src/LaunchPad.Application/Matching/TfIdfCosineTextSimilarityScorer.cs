using System.Text.RegularExpressions;

namespace LaunchPad.Application.Matching;

/// <summary>
/// Lightweight TF-IDF over whatever plain-text corpus the caller provides — no stemming, no
/// external NLP library, cheap enough to rebuild per matching run at cohort scale (30-200
/// documents). Smoothed IDF (ln(N/(1+df))+1) keeps terms unique to one document from
/// dominating and avoids a divide-by-zero/negative weight on terms present in every document.
/// </summary>
public sealed partial class TfIdfCosineTextSimilarityScorer : ITextSimilarityScorer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has", "have", "in",
        "is", "it", "its", "of", "on", "or", "that", "the", "this", "to", "was", "will",
        "with", "we", "you", "your", "our", "their", "they", "i", "he", "she", "them",
    };

    public IPreparedTextSimilarityIndex Prepare(IReadOnlyCollection<string> corpus)
    {
        var documentTokenSets = corpus
            .Select(Tokenize)
            .Where(tokens => tokens.Count > 0)
            .ToList();

        if (documentTokenSets.Count == 0)
        {
            return new PreparedIndex(new Dictionary<string, decimal>());
        }

        var documentFrequency = new Dictionary<string, int>();
        foreach (var tokens in documentTokenSets)
        {
            foreach (var term in tokens.Keys)
            {
                documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
            }
        }

        var n = documentTokenSets.Count;
        var idf = documentFrequency.ToDictionary(
            kvp => kvp.Key,
            kvp => (decimal)(Math.Log((double)n / (1 + kvp.Value)) + 1));

        return new PreparedIndex(idf);
    }

    private static Dictionary<string, int> Tokenize(string? text)
    {
        var counts = new Dictionary<string, int>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return counts;
        }

        foreach (Match match in TokenPattern().Matches(text))
        {
            var token = match.Value.ToLowerInvariant();
            if (token.Length < 3 || StopWords.Contains(token))
            {
                continue;
            }

            counts[token] = counts.GetValueOrDefault(token) + 1;
        }

        return counts;
    }

    [GeneratedRegex(@"[a-zA-Z][a-zA-Z0-9+\-#.]*")]
    private static partial Regex TokenPattern();

    private sealed class PreparedIndex(IReadOnlyDictionary<string, decimal> idf) : IPreparedTextSimilarityIndex
    {
        public decimal CosineSimilarity(string? textA, string? textB)
        {
            if (string.IsNullOrWhiteSpace(textA) || string.IsNullOrWhiteSpace(textB))
            {
                return 0m;
            }

            var vectorA = WeightedVector(textA);
            var vectorB = WeightedVector(textB);
            if (vectorA.Count == 0 || vectorB.Count == 0)
            {
                return 0m;
            }

            decimal dotProduct = 0m;
            foreach (var (term, weightA) in vectorA)
            {
                if (vectorB.TryGetValue(term, out var weightB))
                {
                    dotProduct += weightA * weightB;
                }
            }

            if (dotProduct == 0m)
            {
                return 0m;
            }

            var magnitudeA = (decimal)Math.Sqrt((double)vectorA.Values.Sum(w => w * w));
            var magnitudeB = (decimal)Math.Sqrt((double)vectorB.Values.Sum(w => w * w));
            if (magnitudeA == 0m || magnitudeB == 0m)
            {
                return 0m;
            }

            var similarity = dotProduct / (magnitudeA * magnitudeB);
            return Math.Clamp(similarity, 0m, 1m);
        }

        private Dictionary<string, decimal> WeightedVector(string text)
        {
            var termFrequency = Tokenize(text);
            var vector = new Dictionary<string, decimal>();
            foreach (var (term, count) in termFrequency)
            {
                // Terms outside the prepared corpus (e.g. a candidate's own bio wasn't in
                // Prepare's input) simply don't contribute — no IDF weight to score them with.
                if (idf.TryGetValue(term, out var weight))
                {
                    vector[term] = count * weight;
                }
            }

            return vector;
        }
    }
}
