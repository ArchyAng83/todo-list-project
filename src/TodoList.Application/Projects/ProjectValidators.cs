using FluentValidation;
using TodoList.Domain.Projects;

namespace TodoList.Application.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Project.NameMaxLength);

        RuleFor(x => x.Color)
            .Must(ProjectColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Project.NameMaxLength);

        RuleFor(x => x.Color)
            .Must(ProjectColors.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("Color must be a hex value such as #6B7280.");
    }
}

internal static class ProjectColors
{
    public static bool IsValid(string? color)
        => string.IsNullOrWhiteSpace(color)
            || (color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit));
}
