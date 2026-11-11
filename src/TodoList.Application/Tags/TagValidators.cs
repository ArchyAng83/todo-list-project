using FluentValidation;
using TodoList.Application.Common;
using TodoList.Domain.Tags;

namespace TodoList.Application.Tags;

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(Tag.NameMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Color)
            .Must(TagColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithErrorCode(ValidationCodes.InvalidColor)
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(Tag.NameMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Color)
            .Must(TagColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithErrorCode(ValidationCodes.InvalidColor)
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

internal static class TagColors
{
    public static bool IsValid(string? color)
        => string.IsNullOrWhiteSpace(color)
            || (color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit));
}
