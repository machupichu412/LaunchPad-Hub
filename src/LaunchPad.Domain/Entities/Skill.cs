using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

public class SkillCategory
{
    public int SkillCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Skill> Skills { get; set; } = new List<Skill>();
}

public class Skill
{
    public int SkillId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SkillCategoryId { get; set; }

    public SkillCategory SkillCategory { get; set; } = null!;
}

public class CandidateSkill
{
    public int CandidateId { get; set; }
    public int SkillId { get; set; }
    public byte Proficiency { get; set; } = 3;
    public SkillSource Source { get; set; }

    public Candidate Candidate { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}

public class ProjectSkill
{
    public int ProjectId { get; set; }
    public int SkillId { get; set; }
    public bool IsRequired { get; set; }

    public Project Project { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}
