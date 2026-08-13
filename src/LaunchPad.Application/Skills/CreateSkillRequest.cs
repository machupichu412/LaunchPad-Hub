using FluentValidation;

namespace LaunchPad.Application.Skills;

public class CreateSkillRequest
{
    public string Name { get; set; } = string.Empty;
    public int SkillCategoryId { get; set; }
}

public sealed class CreateSkillRequestValidator : AbstractValidator<CreateSkillRequest>
{
    public CreateSkillRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        RuleFor(r => r.SkillCategoryId).GreaterThan(0);
    }
}
