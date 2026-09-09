using System.Text;

namespace LaunchPad.Application.Skills;

/// <summary>How an extracted skill name was resolved to the taxonomy. Ordered by decreasing
/// confidence; the numeric values are persisted on ParsedCandidateSkill, so don't renumber.</summary>
public enum SkillMatchMethod
{
    None = 0,
    Exact = 1,
    Normalized = 2,
    Alias = 3,
    Affix = 4,
    /// <summary>Semantic similarity. Not produced by SkillNameMatcher — it needs an embedding
    /// client, so it is applied by the caller to whatever this class leaves unresolved.</summary>
    Embedding = 5,
}

/// <summary>A taxonomy entry, reduced to what matching needs. A lightweight record rather than
/// the Skill entity so this stays trivially testable without a SkillCategory graph.</summary>
public readonly record struct TaxonomySkill(int SkillId, string Name);

public sealed record SkillNameMatch(int SkillId, string SkillName, SkillMatchMethod Method, decimal Confidence)
{
    /// <summary>
    /// Whether the confirmation UI may pre-check this row.
    ///
    /// Exact, Normalized and Alias are all cases where two strings provably denote the same
    /// skill — a spelling difference, or a hand-curated synonym. Affix (and later Embedding)
    /// are inferences, so they are shown with the arrow but left unchecked: the candidate opts
    /// in rather than out. This is what keeps a wrong inference from quietly reaching matching.
    /// </summary>
    public bool IsAutoCheckable => Method is SkillMatchMethod.Exact or SkillMatchMethod.Normalized or SkillMatchMethod.Alias;
}

/// <summary>
/// Resolves an extracted skill name against the taxonomy using only deterministic string rules.
///
/// This exists because near-duplicate skills silently break matching: if half the candidates
/// carry "Power BI" and half "PowerBI", the engine sees two unrelated skill ids and neither
/// group matches a project asking for the other. Every variant collapsed here is one that never
/// reaches the taxonomy — and the ones that survive are a much smaller, genuinely novel set for
/// Program Ops to rule on.
///
/// Pure and synchronous by design, per the layering rule: no EF, no HTTP, no embedding client,
/// so the whole reconciliation ladder is unit-testable without infrastructure. Semantic matching
/// is a separate, later step applied by the caller to whatever this leaves unresolved.
/// </summary>
public static class SkillNameMatcher
{
    /// <summary>
    /// Hand-curated synonyms, normalized on both sides. Deliberately small and boring: every
    /// entry is one a person can defend on sight.
    ///
    /// Short ambiguous abbreviations are excluded on purpose — "tf" is TensorFlow or Terraform,
    /// "rn" is React Native or nothing. Guessing wrong there is worse than leaving the name for
    /// a human, because an auto-applied wrong skill silently distorts who gets staffed.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["js"] = "javascript",
        ["ts"] = "typescript",
        ["py"] = "python",
        ["golang"] = "go",
        ["dotnet"] = "net",
        ["objc"] = "objectivec",
        ["k8s"] = "kubernetes",
        ["k8"] = "kubernetes",
        ["postgres"] = "postgresql",
        ["mssql"] = "sqlserver",
        ["msssql"] = "sqlserver",
        ["gcp"] = "googlecloudplatform",
        ["aws"] = "amazonwebservices",
        ["ml"] = "machinelearning",
        ["nlp"] = "naturallanguageprocessing",
        ["cv"] = "computervision",
        ["powerbi"] = "powerbi",
        ["a11y"] = "accessibility",
        ["i18n"] = "internationalization",
    };

    /// <summary>Vendor names that qualify a product without changing which product it is —
    /// "Microsoft Power BI" and "Power BI" are the same skill.</summary>
    private static readonly string[] VendorPrefixes =
        ["microsoft", "apache", "amazon", "google", "oracle", "adobe", "ibm", "meta", "redhat"];

    /// <summary>Suffixes that decorate a technology's name without naming a different one.</summary>
    private static readonly string[] NoiseSuffixes =
        ["js", "framework", "library", "language", "programming", "development"];

    public static SkillNameIndex Build(IEnumerable<TaxonomySkill> taxonomy) => new(taxonomy);

    /// <summary>
    /// Case- and punctuation-insensitive form: lowercased, with everything outside
    /// [a-z0-9+#] removed. This is what collapses "Power BI", "PowerBI" and "power-bi" onto one
    /// key. '+' and '#' survive because dropping them would merge "C", "C++" and "C#".
    /// </summary>
    public static string Normalize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '+' or '#')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    internal static string? ResolveAlias(string normalized) =>
        Aliases.TryGetValue(normalized, out var canonical) && canonical != normalized ? canonical : null;

    /// <summary>Normalized forms to retry after stripping a vendor prefix or a noise suffix.
    /// Returns nothing when stripping would leave too little to be meaningful.</summary>
    internal static IEnumerable<string> AffixVariants(string normalized)
    {
        foreach (var prefix in VendorPrefixes)
        {
            if (normalized.Length > prefix.Length + 2 && normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return normalized[prefix.Length..];
            }
        }

        foreach (var suffix in NoiseSuffixes)
        {
            if (normalized.Length > suffix.Length + 2 && normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                yield return normalized[..^suffix.Length];
            }
        }
    }
}

/// <summary>
/// A taxonomy prepared for repeated lookups. Built once per parse run rather than per skill —
/// same shape as ITextSimilarityScorer.Prepare, and for the same reason.
/// </summary>
public sealed class SkillNameIndex
{
    private readonly Dictionary<string, TaxonomySkill> _byExactName;
    private readonly Dictionary<string, TaxonomySkill> _byNormalizedName;

    public SkillNameIndex(IEnumerable<TaxonomySkill> taxonomy)
    {
        var all = taxonomy.ToList();
        Taxonomy = all;

        _byExactName = new Dictionary<string, TaxonomySkill>(StringComparer.OrdinalIgnoreCase);
        _byNormalizedName = new Dictionary<string, TaxonomySkill>(StringComparer.Ordinal);

        // Skill.Name is uniquely indexed, but only as written — "Power BI" and "PowerBI" can
        // both exist as separate rows, so normalized keys really can collide. Lowest id wins:
        // the older row is the one existing profiles and projects already point at, and the
        // result must not depend on enumeration order.
        foreach (var skill in all.OrderBy(s => s.SkillId))
        {
            var trimmed = skill.Name.Trim();
            _byExactName.TryAdd(trimmed, skill);

            var normalized = SkillNameMatcher.Normalize(skill.Name);
            if (normalized.Length > 0)
            {
                _byNormalizedName.TryAdd(normalized, skill);
            }
        }
    }

    public IReadOnlyList<TaxonomySkill> Taxonomy { get; }

    /// <summary>
    /// Walks the ladder in decreasing-confidence order and returns the first hit, or null when
    /// the name is unresolved by string rules alone — the caller's cue to try semantic matching
    /// and, failing that, to raise it for Program Ops.
    /// </summary>
    public SkillNameMatch? Match(string extractedName)
    {
        if (string.IsNullOrWhiteSpace(extractedName)) return null;

        if (_byExactName.TryGetValue(extractedName.Trim(), out var exact))
        {
            return new SkillNameMatch(exact.SkillId, exact.Name, SkillMatchMethod.Exact, 1.00m);
        }

        var normalized = SkillNameMatcher.Normalize(extractedName);
        if (normalized.Length == 0) return null;

        if (_byNormalizedName.TryGetValue(normalized, out var byNormalized))
        {
            return new SkillNameMatch(byNormalized.SkillId, byNormalized.Name, SkillMatchMethod.Normalized, 0.98m);
        }

        if (SkillNameMatcher.ResolveAlias(normalized) is { } canonical
            && _byNormalizedName.TryGetValue(canonical, out var byAlias))
        {
            return new SkillNameMatch(byAlias.SkillId, byAlias.Name, SkillMatchMethod.Alias, 0.95m);
        }

        foreach (var variant in SkillNameMatcher.AffixVariants(normalized))
        {
            if (_byNormalizedName.TryGetValue(variant, out var byAffix))
            {
                return new SkillNameMatch(byAffix.SkillId, byAffix.Name, SkillMatchMethod.Affix, 0.80m);
            }
        }

        return null;
    }
}
