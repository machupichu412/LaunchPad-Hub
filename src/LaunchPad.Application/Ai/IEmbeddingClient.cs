namespace LaunchPad.Application.Ai;

/// <summary>
/// Turns text into vectors for semantic similarity — both the matching engine's text signal
/// and skill-name reconciliation.
///
/// Batched by design. A cohort matching run embeds every candidate and project document at
/// once, and skill reconciliation embeds every unresolved name from a resume at once; a
/// one-string-at-a-time contract would turn either into an N-call storm against a rate-limited
/// endpoint.
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// Returns one vector per input, in input order. Implementations must preserve order and
    /// arity — callers zip the result back against their own inputs positionally.
    /// </summary>
    /// <returns>
    /// An empty list when embeddings are unavailable. Callers treat that as "no similarity
    /// signal" and fall back to a neutral score rather than failing — text similarity is a
    /// bonus in the composite, never a gate.
    /// </returns>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);

    /// <summary>
    /// The model these vectors come from, e.g. "text-embedding-3-small". Part of the cache key:
    /// vectors from different models are not comparable, so a model change must miss the cache
    /// rather than silently mixing incompatible vectors into one similarity computation.
    /// </summary>
    string ModelName { get; }
}
