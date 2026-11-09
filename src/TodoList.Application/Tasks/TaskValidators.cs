using FluentValidation;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(TodoTask.TitleMaxLength);
    }
}

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(TodoTask.TitleMaxLength);

        RuleFor(x => x.Notes)
            .MaximumLength(TodoTask.NotesMaxLength);

        RuleFor(x => x.ReminderOffsetMinutes)
            .InclusiveBetween(0, TodoTask.MaxReminderOffsetMinutes)
            .When(x => x.ReminderOffsetMinutes.HasValue);

        RuleFor(x => x.DueTime)
            .Null()
            .When(x => x.DueDate is null)
            .WithMessage("A due time requires a due date.");

        RuleFor(x => x.ReminderOffsetMinutes)
            .Null()
            .When(x => x.DueDate is null)
            .WithMessage("A reminder requires a due date.");
    }
}

public sealed class MoveTaskRequestValidator : AbstractValidator<MoveTaskRequest>
{
    public MoveTaskRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.TargetIndex).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddChecklistItemRequestValidator : AbstractValidator<AddChecklistItemRequest>
{
    public AddChecklistItemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(ChecklistItem.TitleMaxLength);
    }
}

public sealed class AddTaskTagRequestValidator : AbstractValidator<AddTaskTagRequest>
{
    public AddTaskTagRequestValidator()
    {
        RuleFor(x => x.TagId).NotEmpty();
    }
}
