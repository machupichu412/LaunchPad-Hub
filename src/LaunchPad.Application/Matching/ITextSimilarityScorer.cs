namespace LaunchPad.Application.Matching;

/// <summary>
/// Pure TF-IDF cosine similarity over a fixed corpus, no I/O. Prepare(corpus) builds one IDF
/// index from every document the caller expects to compare in a run (e.g. all eligible
/// candidate bios plus the relevant project description(s)), then CosineSimilarity is cheap
/// to call repeatedly for every project x candidate pair against that shared index — avoids
/// recomputing document frequencies per pair. Swapping the text source later (e.g. real
/// extracted resume text) means changing what's fed into Prepare, not this interface.
/// </summary>
public interface ITextSimilarityScorer
{
    IPreparedTextSimilarityIndex Prepare(IReadOnlyCollection<string> corpus);
}

public interface IPreparedTextSimilarityIndex
{
    /// <summary>0-1. Returns 0 if either text is null/blank or the two share no scored terms.</summary>
    decimal CosineSimilarity(string? textA, string? textB);
}
