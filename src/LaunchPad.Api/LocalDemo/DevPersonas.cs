using LaunchPad.Application.Common;

namespace LaunchPad.Api.LocalDemo;

public sealed record DevPersona(string Key, Guid EntraObjectId, string DisplayName, string Upn, string[] Roles);

/// <summary>
/// Fixed identities for local multi-role testing (see DevPersonaAuthHandler). Object ids
/// are constants so the seeded users in LocalDemoSeeder are the same people a tester
/// signs in as. Mirrored in src/LaunchPad.Web/src/dev/devPersonas.ts — keep the keys in step.
/// </summary>
public static class DevPersonas
{
    public static readonly DevPersona Ops = new("ops", Guid.Parse("00000000-0000-4000-8000-000000000001"), "Olivia Ops", "olivia.ops@example.com", [Roles.ProgramOps]);
    public static readonly DevPersona Exec = new("exec", Guid.Parse("00000000-0000-4000-8000-000000000002"), "Evan Exec", "evan.exec@example.com", [Roles.Executive]);
    public static readonly DevPersona HiringManager = new("hm", Guid.Parse("00000000-0000-4000-8000-000000000003"), "Harper Hiring", "harper.hiring@example.com", [Roles.HiringManager]);
    public static readonly DevPersona Sponsor = new("sponsor", Guid.Parse("00000000-0000-4000-8000-000000000010"), "Sam Sponsor", "sponsor.demo@example.com", [Roles.Sponsor]);
    public static readonly DevPersona Sponsor2 = new("sponsor2", Guid.Parse("00000000-0000-4000-8000-000000000011"), "Priya Shah", "priya.shah@example.com", [Roles.Sponsor]);
    public static readonly DevPersona SponsorNew = new("sponsor-new", Guid.Parse("00000000-0000-4000-8000-000000000012"), "Nia Newsponsor", "nia.newsponsor@example.com", [Roles.Sponsor]);
    public static readonly DevPersona Candidate1 = new("cand1", Guid.Parse("00000000-0000-4000-8000-000000000020"), "Jordan Rivera", "jordan.rivera@example.com", [Roles.Candidate]);
    public static readonly DevPersona Candidate2 = new("cand2", Guid.Parse("00000000-0000-4000-8000-000000000021"), "Casey Kim", "casey.kim@example.com", [Roles.Candidate]);
    public static readonly DevPersona Candidate3 = new("cand3", Guid.Parse("00000000-0000-4000-8000-000000000022"), "Morgan Lee", "morgan.lee@example.com", [Roles.Candidate]);
    public static readonly DevPersona CandidateCohort2 = new("cand-c2", Guid.Parse("00000000-0000-4000-8000-000000000023"), "Alex Torres", "alex.torres@example.com", [Roles.Candidate]);
    public static readonly DevPersona CandidateNew = new("cand-new", Guid.Parse("00000000-0000-4000-8000-000000000024"), "Riley Newcandidate", "riley.new@example.com", [Roles.Candidate]);
    public static readonly DevPersona OpsSponsor = new("ops-sponsor", Guid.Parse("00000000-0000-4000-8000-000000000030"), "Dana Dualrole", "dana.dual@example.com", [Roles.ProgramOps, Roles.Sponsor]);
    public static readonly DevPersona NoRole = new("norole", Guid.Parse("00000000-0000-4000-8000-000000000040"), "Nobody Noroles", "nobody@example.com", []);

    public static readonly IReadOnlyDictionary<string, DevPersona> ByKey = new[]
    {
        Ops, Exec, HiringManager, Sponsor, Sponsor2, SponsorNew,
        Candidate1, Candidate2, Candidate3, CandidateCohort2, CandidateNew, OpsSponsor, NoRole,
    }.ToDictionary(p => p.Key, StringComparer.Ordinal);
}
