using FluentAssertions;
using LaunchPad.Application.Skills;
using Xunit;

namespace LaunchPad.Application.Tests.Skills;

public class SkillNameMatcherTests
{
    private static SkillNameIndex IndexOf(params string[] names) =>
        SkillNameMatcher.Build(names.Select((n, i) => new TaxonomySkill(i + 1, n)));

    // --- Normalization ------------------------------------------------------

    [Theory]
    [InlineData("Power BI", "powerbi")]
    [InlineData("PowerBI", "powerbi")]
    [InlineData("power-bi", "powerbi")]
    [InlineData("  Power  BI  ", "powerbi")]
    [InlineData("POWER.BI", "powerbi")]
    public void Normalize_CollapsesThePowerBiVariants(string input, string expected) =>
        SkillNameMatcher.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_KeepsPlusAndHash_SoCFamilyLanguagesStayDistinct()
    {
        // Dropping these would merge three unrelated skills into one.
        var c = SkillNameMatcher.Normalize("C");
        var cpp = SkillNameMatcher.Normalize("C++");
        var csharp = SkillNameMatcher.Normalize("C#");

        new[] { c, cpp, csharp }.Should().OnlyHaveUniqueItems();
    }

    // --- The case this whole class exists for -------------------------------

    [Theory]
    [InlineData("PowerBI")]
    [InlineData("power-bi")]
    [InlineData("power bi")]
    [InlineData("Power.BI")]
    public void Match_ResolvesPowerBiVariantsToTheExistingSkill(string extracted)
    {
        // CLAUDE.md names this exact trio as the pain the rebuild exists to fix: variants that
        // look identical to a person but are separate ids to the matching engine.
        var match = IndexOf("Power BI").Match(extracted);

        match.Should().NotBeNull();
        match!.SkillName.Should().Be("Power BI");
        match.IsAutoCheckable.Should().BeTrue();
    }

    // --- Negatives: the expensive kind of mistake ---------------------------

    [Fact]
    public void Match_DoesNotCollapseJavaIntoJavaScript()
    {
        // The canonical false positive. These are different skills, and auto-applying the wrong
        // one silently distorts who gets staffed onto what.
        var index = IndexOf("JavaScript");

        index.Match("Java").Should().BeNull();
    }

    [Fact]
    public void Match_PrefersTheExactSkillWhenBothJavaAndJavaScriptExist()
    {
        var index = IndexOf("Java", "JavaScript");

        index.Match("Java")!.SkillName.Should().Be("Java");
        index.Match("JavaScript")!.SkillName.Should().Be("JavaScript");
    }

    [Theory]
    [InlineData("SQL", "SQL Server")]      // a substring is not a synonym
    [InlineData("React", "React Native")]  // nor is a qualified variant
    [InlineData("Azure", "Azure DevOps")]
    [InlineData("Go", "MongoDB")]
    public void Match_DoesNotMatchOnMerePrefixOrSubstring(string extracted, string taxonomyEntry) =>
        IndexOf(taxonomyEntry).Match(extracted).Should().BeNull();

    [Fact]
    public void Match_UnknownSkill_ReturnsNull_SoItCanBeRaisedForOps()
    {
        IndexOf("React", "TypeScript").Match("Rust").Should().BeNull();
    }

    // --- Aliases ------------------------------------------------------------

    [Theory]
    [InlineData("k8s", "Kubernetes")]
    [InlineData("K8s", "Kubernetes")]
    [InlineData("JS", "JavaScript")]
    [InlineData("Postgres", "PostgreSQL")]
    [InlineData("MS SQL", "SQL Server")]
    [InlineData("golang", "Go")]
    [InlineData("ML", "Machine Learning")]
    public void Match_ResolvesCuratedAliases(string extracted, string taxonomyEntry)
    {
        var match = IndexOf(taxonomyEntry).Match(extracted);

        match.Should().NotBeNull();
        match!.SkillName.Should().Be(taxonomyEntry);
        match.Method.Should().Be(SkillMatchMethod.Alias);
        match.IsAutoCheckable.Should().BeTrue("a hand-curated synonym is as certain as a spelling variant");
    }

    [Fact]
    public void Match_AmbiguousAbbreviationsAreDeliberatelyNotAliased()
    {
        // "TF" is TensorFlow or Terraform; "RN" is React Native or nothing. Leaving these for a
        // human is the point — a confident wrong answer is worse than no answer here.
        IndexOf("Terraform").Match("TF").Should().BeNull();
        IndexOf("TensorFlow").Match("TF").Should().BeNull();
        IndexOf("React Native").Match("RN").Should().BeNull();
    }

    // --- Affix stripping ----------------------------------------------------

    [Theory]
    [InlineData("Microsoft Power BI", "Power BI")]
    [InlineData("Apache Kafka", "Kafka")]
    [InlineData("Google Kubernetes", "Kubernetes")]
    public void Match_StripsVendorPrefixes(string extracted, string taxonomyEntry)
    {
        var match = IndexOf(taxonomyEntry).Match(extracted);

        match.Should().NotBeNull();
        match!.SkillName.Should().Be(taxonomyEntry);
        match.Method.Should().Be(SkillMatchMethod.Affix);
    }

    [Theory]
    [InlineData("React.js", "React")]
    [InlineData("AngularJS", "Angular")]
    public void Match_StripsNoiseSuffixes(string extracted, string taxonomyEntry) =>
        IndexOf(taxonomyEntry).Match(extracted)!.Method.Should().Be(SkillMatchMethod.Affix);

    [Fact]
    public void Match_AffixMatchesAreNotAutoCheckable()
    {
        // Stripping an affix is an inference, not a proof of sameness — "AngularJS" and
        // "Angular" really are different frameworks. Surfaced with the arrow, left unchecked, so
        // the candidate opts in rather than out.
        IndexOf("Angular").Match("AngularJS")!.IsAutoCheckable.Should().BeFalse();
    }

    [Fact]
    public void Match_DoesNotStripAnAffixDownToAStub()
    {
        // Guards against "Meta" -> "" or a two-character remnant matching something unrelated.
        IndexOf("A", "Ta").Match("Meta").Should().BeNull();
    }

    // --- Determinism and ordering ------------------------------------------

    [Fact]
    public void Match_ExactBeatsNormalized_WhenBothCouldApply()
    {
        // "Power BI" is present verbatim, so it must win over the "PowerBI" row regardless of
        // which was inserted first.
        IndexOf("PowerBI", "Power BI").Match("Power BI")!
            .Method.Should().Be(SkillMatchMethod.Exact);
    }

    [Fact]
    public void Match_CollidingNormalizedNames_ResolveToTheOlderSkill_RegardlessOfOrder()
    {
        // Both rows can legitimately exist: Skill.Name is unique only as written. The result
        // must not depend on enumeration order, or two candidates parsed in different sessions
        // could land on different ids for the same word.
        var ascending = SkillNameMatcher.Build([new TaxonomySkill(7, "Power BI"), new TaxonomySkill(42, "PowerBI")]);
        var descending = SkillNameMatcher.Build([new TaxonomySkill(42, "PowerBI"), new TaxonomySkill(7, "Power BI")]);

        ascending.Match("power-bi")!.SkillId.Should().Be(7);
        descending.Match("power-bi")!.SkillId.Should().Be(7);
    }

    [Fact]
    public void Match_IsCaseInsensitiveOnExactNames()
    {
        IndexOf("TypeScript").Match("typescript")!.SkillName.Should().Be("TypeScript");
    }

    // --- Degenerate input ---------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("---")]
    public void Match_DegenerateInput_ReturnsNullRatherThanThrowing(string extracted) =>
        IndexOf("React").Match(extracted).Should().BeNull();

    [Fact]
    public void Build_EmptyTaxonomy_MatchesNothingButDoesNotThrow()
    {
        var index = SkillNameMatcher.Build(Array.Empty<TaxonomySkill>());

        index.Match("React").Should().BeNull();
        index.Taxonomy.Should().BeEmpty();
    }

    [Fact]
    public void Build_TaxonomyEntryThatNormalizesToNothing_IsSkippedNotCrashed()
    {
        // A punctuation-only skill name shouldn't become a wildcard key that swallows every
        // unmatched extraction.
        var index = SkillNameMatcher.Build([new TaxonomySkill(1, "---"), new TaxonomySkill(2, "React")]);

        index.Match("Rust").Should().BeNull();
        index.Match("React")!.SkillId.Should().Be(2);
    }
}
