namespace LaunchPad.Application.Skills;

public class SkillDto
{
    public int SkillId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SkillCategoryId { get; set; }
    public string SkillCategoryName { get; set; } = string.Empty;
}

public class SkillCategoryDto
{
    public int SkillCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}
