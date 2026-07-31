using FluentValidation;

namespace LaunchPad.Application.Community;

public sealed class CreateCommunityPostRequestValidator : AbstractValidator<CreateCommunityPostRequest>
{
    public CreateCommunityPostRequestValidator()
    {
        RuleFor(r => r.Body).NotEmpty().MaximumLength(2000);
        RuleFor(r => r.PostType).IsInEnum();
    }
}

public sealed class CreateCommunityCommentRequestValidator : AbstractValidator<CreateCommunityCommentRequest>
{
    public CreateCommunityCommentRequestValidator()
    {
        RuleFor(r => r.Body).NotEmpty().MaximumLength(1000);
    }
}
