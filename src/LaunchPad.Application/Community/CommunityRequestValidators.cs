using FluentValidation;

namespace LaunchPad.Application.Community;

public sealed class CreateCommunityPostRequestValidator : AbstractValidator<CreateCommunityPostRequest>
{
    // 8 MB — a feed image, not a document (compare SubmitDeliverableRequestValidator's 100MB).
    public const long MaxImageBytes = 8 * 1024 * 1024;
    public static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
    };

    public CreateCommunityPostRequestValidator()
    {
        RuleFor(r => r.Body).NotEmpty().MaximumLength(2000);
        RuleFor(r => r.PostType).IsInEnum();

        // Image is optional — these rules only engage when the caller actually attached a
        // file (ImageContentType is set by the controller from the bound IFormFile).
        When(r => r.ImageContentType is not null, () =>
        {
            RuleFor(r => r.ImageContentType)
                .Must(ct => AllowedImageContentTypes.Contains(ct!))
                .WithMessage("Unsupported image type. Use JPEG, PNG, or WebP.");
            RuleFor(r => r.ImageLength)
                .GreaterThan(0).WithMessage("The selected image is empty.")
                .LessThanOrEqualTo(MaxImageBytes).WithMessage("That image is too large — maximum size is 8 MB.");
        });
    }
}

public sealed class CreateCommunityCommentRequestValidator : AbstractValidator<CreateCommunityCommentRequest>
{
    public CreateCommunityCommentRequestValidator()
    {
        RuleFor(r => r.Body).NotEmpty().MaximumLength(1000);
    }
}
