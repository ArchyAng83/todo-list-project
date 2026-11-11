using FluentValidation;
using TodoList.Application.Common;
using TodoList.Domain.Projects;

namespace TodoList.Application.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(Project.NameMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Color)
            .Must(ProjectColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithErrorCode(ValidationCodes.InvalidColor)
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(Project.NameMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Color)
            .Must(ProjectColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithErrorCode(ValidationCodes.InvalidColor)
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

internal static class ProjectColors
{
    public static bool IsValid(string? color)
        => string.IsNullOrWhiteSpace(color)
            || (color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit));
}
