using FluentValidation;
using TodoList.Domain.Tags;

namespace TodoList.Application.Tags;

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Tag.NameMaxLength);

        RuleFor(x => x.Color)
            .Must(TagColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Tag.NameMaxLength);

        RuleFor(x => x.Color)
            .Must(TagColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

internal static class TagColors
{
    public static bool IsValid(string? color)
        => string.IsNullOrWhiteSpace(color)
            || (color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit));
}
